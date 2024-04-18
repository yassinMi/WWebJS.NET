using Grpc.Net.Client;
using System.Net.Http;
using WWebJsService;
using System.Diagnostics;
using System.Text;

namespace WWebJS.NET;

public enum WorkerStatus
{
    Connected,
    Error,
    Connecting
}
public class WWebJSWorker : IDisposable
{
    static WWebJSWorker()
    {
        //workaround grpc-js-namedppipes not supporting transmission mode Message:
        //we force the dotnet package to use Byte
        //todo: fix this
        try
        {
            var t = typeof(GrpcDotNetNamedPipes.NamedPipeServer).Assembly.GetType("GrpcDotNetNamedPipes.Internal.PlatformConfig");
            var pi1 = t!.GetField("<TransmissionMode>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            pi1!.SetValue(null, System.IO.Pipes.PipeTransmissionMode.Byte);
            var pi2 = t.GetField("<SizePrefix>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            pi2!.SetValue(null, true);
        }
        catch (Exception err)
        {
            throw new Exception("WWebJS.NET: failed to access private fields with reflection, this error indicates internal changes in GrpcDotNetNamedPipes that breaks a temporary workaround", err);
        }

    }

    public Action<string> Log { get; set; } = DefaultLog;

    private static void DefaultLog(string str)
    {
        Console.WriteLine(str);
    }

    public event DataReceivedEventHandler ProcessOutputDataReceived;
    public event DataReceivedEventHandler ProcessErrorDataReceived;

    private WWebJsService.WWebJsService.WWebJsServiceClient _Client;
    public WWebJsService.WWebJsService.WWebJsServiceClient Client
    {

        get
        {
            if (Status != WorkerStatus.Connected) throw new InvalidOperationException($"Cannot use the worker instance, status='{Status}'");
            return _Client;
        }
        private set { _Client = value; }
    }
    public WWebJSWorker(WWebJSWorkerStartInfo workerStartInfo)
    {

        NamedPipeName = Guid.NewGuid().ToString();

        this.WorkerStartInfo = workerStartInfo;

    }

    public async Task Start()
    {

        try
        {
            bool isPackagedMode = false;
            try
            {
                this.WorkerStartInfo.ValidateCanStartWithPackagedExecutable();
                isPackagedMode = true;
            }
            catch (System.Exception)
            {
                this.WorkerStartInfo.ValidateCanStartWithNode();
            }
            if (isPackagedMode)
            {
                if (!File.Exists(WorkerStartInfo.PackagedExecutablePath)) throw new Exception($"packaged executable not found: '{WorkerStartInfo.PackagedExecutablePath}'");

                var workerArgs = new string[] { NamedPipeName };
                Log($"attempting to run '{WorkerStartInfo.PackagedExecutablePath}' with args [{string.Join(",", workerArgs)}]...");

                Status = WorkerStatus.Connecting;
                var processStarted = await StartWorkerWithArgs(WorkerStartInfo.PackagedExecutablePath,workerArgs);
                if (!processStarted) throw new Exception("cannot start process");

            }
            else{
                //running node.exe 
                if (!File.Exists(WorkerStartInfo.NodeExecutablePath)) throw new Exception($"node.exe not found: '{WorkerStartInfo.NodeExecutablePath}'");
                if(!Path.GetFileName(WorkerStartInfo.NodeExecutablePath).Equals("node.exe", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"invalid node path: '{WorkerStartInfo.NodeExecutablePath}'");
                }
                if(!Directory.Exists(WorkerStartInfo.NodeAppDirectory))
                {
                    throw new Exception($"wds app directory not found or is not valid at : '{WorkerStartInfo.NodeAppDirectory}'");
                }
                if(!Directory.Exists(Path.Combine(WorkerStartInfo.NodeAppDirectory,"node_modules")))
                {
                    throw new Exception($"wds app directory not found or is not valid at : '{WorkerStartInfo.NodeAppDirectory}'");
                }
                var indexJsPath = Path.Combine(WorkerStartInfo.NodeAppDirectory,"index.js");
                if(!File.Exists(indexJsPath))
                {
                    throw new Exception($"index.js expected in app directory : '{WorkerStartInfo.NodeAppDirectory}'");
                }
                //todo: validate package.json version to ensure compatibility

                var workerArgs = new string[] {indexJsPath , NamedPipeName };
                Log($"attempting to run '{WorkerStartInfo.PackagedExecutablePath}' with args [{string.Join(",", workerArgs)}]...");

                Status = WorkerStatus.Connecting;
                var processStarted = await StartWorkerWithArgs(WorkerStartInfo.NodeExecutablePath, workerArgs);
                if (!processStarted) throw new Exception("cannot start process");

            }



            var channel = new GrpcDotNetNamedPipes.NamedPipeChannel(".", NamedPipeName);
            Client = new WWebJsService.WWebJsService.WWebJsServiceClient(channel);
            Log($"attempting to get ping resp...");
            if (await TryGetPingResponse()) Status = WorkerStatus.Connected;
            else throw new Exception("cannot start process, ping err");
        }
        catch (System.Exception err)
        {

            Log(err.ToString());
            SetError(err);
            throw;
        }

    }

    async Task<bool> TryGetPingResponse()
    {
        return await Task.FromResult(true);
    }
    void SetError(Exception err)
    {
        this.Status = WorkerStatus.Error;
        this.Error = err;
    }
    public Exception Error { get; private set; }
    string NamedPipeName { get; set; }
    public WWebJSWorkerStartInfo WorkerStartInfo { get; }

    Process process;

    ///<summary>
    /// runs the specified <paramref name="program"/> (either node or the packaged exe, and passes the <paramref name="args"/>)
    /// resolves when recieving <see langword="abstract"/> "Server listening" stdout line 
    ///</summary>
    private async Task<bool> StartWorkerWithArgs(string program, string[] args)
    {
        var exePath = program;
        var tcs = new TaskCompletionSource<bool>();
        StringBuilder startupLogs = new StringBuilder();
        try
        {
            process = new Process
            {
                StartInfo =
            {
                FileName = exePath,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false
            },
                EnableRaisingEvents = true
            };

            DataReceivedEventHandler? onData = null; ;
            DataReceivedEventHandler? onError = null;
            onData = (s, e) =>
            {
                if (e.Data != null && e.Data.Contains("Server listening"))
                {
                    startupLogs.AppendLine(e.Data);
                    process.OutputDataReceived -= onData;
                    process.ErrorDataReceived -= onError;
                    tcs.TrySetResult(true);
                }
            };
            onError = (s, e) =>
            {
                if (e.Data != null)
                {
                    startupLogs.AppendLine(e.Data);
                    Log($"Worker Error: {e.Data}");
                }
            };
            process.OutputDataReceived += onData;
            process.ErrorDataReceived += onError;
            process.OutputDataReceived += (s, e) =>
            {
                try { ProcessOutputDataReceived?.Invoke(this, e); } catch (System.Exception) { }
            };
            process.ErrorDataReceived += (s, e) =>
            {
                try { ProcessErrorDataReceived?.Invoke(this, e); } catch (System.Exception) { }
            };

            process.Exited += (sender, e) =>
            {
                if (!tcs.Task.IsCompleted)
                {
                    process.OutputDataReceived -= onData;
                    process.ErrorDataReceived -= onError;
                    tcs.TrySetException(new Exception($"process exited before starting server, log: {startupLogs.ToString()}"));
                }
            };

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await tcs.Task;

            return tcs.Task.Result;
        }
        catch (Exception err)
        {
            throw new Exception("cannot start process", err);
        }
    }


    public void Dispose()
    {

        //Client.Dispose();
        if (process != null)
            process.Dispose();


    }
    public void Close()
    {
        Dispose();
    }
    public WorkerStatus Status { get; set; }
    public static object hello()
    {

        return "v1";
    }
}




