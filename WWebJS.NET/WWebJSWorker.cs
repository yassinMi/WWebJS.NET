using Grpc.Net.Client;
using System.Net.Http;
using WWebJsService;
using System.Diagnostics;
using System.Text;
using System.IO.Pipes;

namespace WWebJS.NET;

public enum WorkerStatus
{
    Connected,
    Error,//the worker is running but is in error state and needs to be closed
    Connecting,//the state between calling Start and having the named pipe set up
    Closed//worker (process) has exited (any reason)
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
    /// <summary>
    /// Create an anonymous worker that will automatically close on disposal or after the process ends
    /// </summary>
    /// <param name="workerStartInfo"></param>
    public WWebJSWorker(WWebJSWorkerStartInfo workerStartInfo)
    {
        NamedPipeName = Guid.NewGuid().ToString();
        this.WorkerStartInfo = workerStartInfo;
    }
    /// <summary>
    /// Create a global worker with the specified name or connect to an existing one. NOTE: only use this constructor the worker is intended to be re-used across application instances
    /// </summary>
    /// <param name="workerStartInfo"></param>
    /// <param name="serverName">unique name</param>
    public WWebJSWorker(WWebJSWorkerStartInfo workerStartInfo, string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            throw new ArgumentException("server name cannot be null or empty");
        NamedPipeName = serverName;
        this.WorkerStartInfo = workerStartInfo;
        IsGlobal = true;
    }

    public Action<string> Log { get; set; } = DefaultLog;
    public Exception? Error { get; private set; }
    string NamedPipeName { get; set; }
    public WWebJSWorkerStartInfo WorkerStartInfo { get; }
    public event EventHandler? StatusChanged;
    public static int GlobalWorkerConnectionTimeout { get; set; } = 2000;



    public event DataReceivedEventHandler? ProcessOutputDataReceived;
    public event DataReceivedEventHandler? ProcessErrorDataReceived;

    private WWebJsService.WWebJsService.WWebJsServiceClient? _Client;
    public WWebJsService.WWebJsService.WWebJsServiceClient? Proxy
    {
        get
        {
            if (Status != WorkerStatus.Connected) throw new InvalidOperationException($"Cannot use the worker instance, status='{Status}'");
            return _Client;
        }
        private set { _Client = value; }
    }

    public bool IsGlobal { get; private set; }
    ///<summary>
    /// gets a value indicating whether this instance created new global server instance (only applicable for Global mode and after a successful Start)
    ///</summary>
    public bool IsCreator { get; private set; }
    Process? process;

    Task? startingTask;
    void LogStr(string str)
    {
        if (Log != null)
            Log.Invoke(str);
    }
    private static void DefaultLog(string str)
    {
        Console.WriteLine(str);
    }
    ///<summary>
    ///start the node process (named pipe server), or attempt to connect to an existing one if applicable
    ///</summary>
    public Task Start()
    {
        lock (this)
        {
            throwIfDisposed();
            if (startingTask != null) return startingTask;
            startingTask = Start_impl();
            return startingTask;
        }
    }

    void throwIfDisposed()
    {
        if (isDisposed) throw new InvalidOperationException("worker instance disposed");
    }

    private async Task Start_impl()
    {
        try
        {
            bool isPackagedMode = false;
            try
            {
                this.WorkerStartInfo.ValidateCanStartWithPackagedExecutable(false);
                isPackagedMode = true;
            }
            catch (System.Exception)
            {
                this.WorkerStartInfo.ValidateCanStartWithNode(false);
            }
            if (IsGlobal)
            {
                //detecting existing server
                OnStatusChange(WorkerStatus.Connecting);
                var chOpt_ = new GrpcDotNetNamedPipes.NamedPipeChannelOptions() { ConnectionTimeout = GlobalWorkerConnectionTimeout };
                var ch_ = new GrpcDotNetNamedPipes.NamedPipeChannel(".", NamedPipeName, chOpt_);
                var proxy = new WWebJsService.WWebJsService.WWebJsServiceClient(ch_);
                if (await TryGetPingResponse(proxy) == true)
                {
                    Proxy = proxy;
                    IsCreator = false;
                    OnStatusChange(WorkerStatus.Connected);
                    return;
                }
                IsCreator = true;
            }
            if (isPackagedMode)
            {
                this.WorkerStartInfo.ValidateCanStartWithPackagedExecutable(true);
                var workerArgs = new string[] { NamedPipeName, IsGlobal ? "" : "--monitor" };
                LogStr($"running '{WorkerStartInfo.PackagedExecutablePath}' with args [{string.Join(",", workerArgs)}]...");
                OnStatusChange(WorkerStatus.Connecting);
                var processStarted = await StartWorkerWithArgs(WorkerStartInfo.PackagedExecutablePath!, workerArgs);
                if (!processStarted) throw new Exception("cannot start process");
            }
            else
            {
                this.WorkerStartInfo.ValidateCanStartWithNode(true);
                //running node.exe 
                var indexJsPath = Path.Combine(WorkerStartInfo.NodeAppDirectory!, WWebJSWorkerStartInfo.RelativeEntryPointFile);
                //todo: validate package.json version to ensure compatibility
                var workerArgs = new string[] { indexJsPath, NamedPipeName, IsGlobal ? "" : "--monitor" };
                LogStr($"running '{WorkerStartInfo.PackagedExecutablePath}' with args [{string.Join(",", workerArgs)}]...");
                OnStatusChange(WorkerStatus.Connecting);
                var processStarted = await StartWorkerWithArgs(WorkerStartInfo.NodeExecutablePath!, workerArgs);
                if (!processStarted) throw new Exception("cannot start process");
            }
            var channel = new GrpcDotNetNamedPipes.NamedPipeChannel(".", NamedPipeName);
            Proxy = new WWebJsService.WWebJsService.WWebJsServiceClient(channel);
            LogStr($"ping...");
            if (await TryGetPingResponse(_Client))
            {
                OnStatusChange(WorkerStatus.Connected);
                if (!IsGlobal)
                {
                    heartBeatThread = new Thread(heartBeatFn);
                    heartBeatThread.IsBackground = true;
                    heartbeatCts = new CancellationTokenSource();
                    heartBeatThread.Start();
                }
            }
            else throw new Exception("cannot start process, ping err");

            process!.Exited += hndlProcessExited;
        }
        catch (System.Exception err)
        {
            LogStr(err.ToString());
            SetError(err);
            throw;
        }

    }


    CancellationTokenSource? heartbeatCts;
    /// <summary>
    /// The interval at which heartbeat signals are sent to the server (only used for anonymous type)
    /// </summary>
    static public int HeartbeatIntervalMs { get; set; } = 1000;
    private void heartBeatFn(object? obj)
    {
        try
        {
            NamedPipeClientStream client = new NamedPipeClientStream(this.NamedPipeName + "-mi6d693136");
            client.Connect(5000);
            while (true)
            {

                heartbeatCts!.Token.ThrowIfCancellationRequested();
                client.WriteByte((byte)'1');
                client.Flush();
                Task.Delay(HeartbeatIntervalMs, heartbeatCts!.Token).GetAwaiter().GetResult();
            }
        }
        catch (System.Exception err)
        {
            heartBeatThread = null;
            LogStr(err.ToString());
        }

    }

    private void hndlProcessExited(object? sender, EventArgs e)
    {
        LogStr($"worker_ process existed with code ${process!.ExitCode} , exit time: {process.ExitTime}");
        OnStatusChange(WorkerStatus.Closed);
    }

    async Task<bool> TryGetPingResponse(WWebJsService.WWebJsService.WWebJsServiceClient proxy)
    {
        if (proxy == null) throw new InvalidOperationException("Proxy null");
        try
        {
            return (await proxy.PingAsync(new PingRequest() { Text = "hello" })).Text == "hello";
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    void SetError(Exception err)
    {
        this.Error = err;
        OnStatusChange(WorkerStatus.Error);
    }

    void OnStatusChange(WorkerStatus status)
    {
        if (Status == status) return;
        Status = status;
        Task.Run(() =>
        {//avoiding a deadlock if the app uses dispatcher.invoke in handling this event while blocking on a Dispose() calls
            StatusChanged?.Invoke(this, EventArgs.Empty);
        });
    }



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
                RedirectStandardInput = true,
                CreateNoWindow = WorkerStartInfo.CreateNoWindow,

            },
                EnableRaisingEvents = true
            };
            DataReceivedEventHandler? onData = null;
            DataReceivedEventHandler? onError = null;
            EventHandler? hndlExited = null;
            onData = (s, e) =>
            {
                if (e.Data != null && e.Data.Contains("Server listening"))
                {
                    startupLogs.AppendLine(e.Data);
                    process.OutputDataReceived -= onData;
                    process.ErrorDataReceived -= onError;
                    process.Exited -= hndlExited;
                    tcs.TrySetResult(true);
                }
            };
            onError = (s, e) =>
            {
                if (e.Data != null)
                {
                    startupLogs.AppendLine(e.Data);
                    LogStr($"Worker Error: {e.Data}");
                }
            };
            hndlExited = (sender, e) =>
            {
                LogStr($"worker process existed with code ${process.ExitCode} , exit time: {process.ExitTime}");
                process.OutputDataReceived -= onData;
                process.ErrorDataReceived -= onError;
                process.Exited -= hndlExited;
                if (!tcs.Task.IsCompleted)
                {
                    OnStatusChange(WorkerStatus.Closed);
                    tcs.TrySetException(new Exception($"process exited before starting server, log: {startupLogs.ToString()}"));
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
            process.Exited += hndlExited;
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

    bool isDisposed;
    private Thread? heartBeatThread;
    /// <summary>
    /// For global type, this does nothing. for anonymous type this is equivalent to <see cref="Close"/>
    /// </summary>
    public void Dispose()
    {
        if (isDisposed) return;
        try
        {
            if (IsGlobal) return;
            Close();
        }
        catch (Exception err)
        {
            LogStr($"disposing threw: {err.ToString()}");
        }
        isDisposed = true;
    }
    /// <summary>
    /// Attempt to close the associated Node server, throws exceptions if something went wrong
    /// </summary>
    public void Close()
    {
        LogStr("closing worker");
        if (IsGlobal && !IsCreator)
        {
            //# sending exist signal over the channel
            if (_Client == null) throw new InvalidOperationException("Proxy null, make sure start is called successfully before closing");
            _Client.Exit(new ExitRequest() { Force = true });
        }
        else
        {
            //Client.Dispose();
            if (process != null && !process.HasExited)
            {
                LogStr("worker disposing: close worker");
                process.StandardInput.Write("q");//close gracefully;
                var exited = process.WaitForExit(1000);
                if (!exited)
                {
                    LogStr("worker disposing: !exited");
                    process.Kill();//kill after a timeout
                }
                else
                {
                    LogStr("worker disposing: exited");
                }
                heartbeatCts?.Cancel();
                process.Dispose();
                process = null;
            }
        }

    }
    public WorkerStatus Status { get; set; }
    public static object hello()
    {
        return "v1";
    }
}




