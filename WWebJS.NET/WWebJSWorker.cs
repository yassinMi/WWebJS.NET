using Grpc.Net.Client;
using System.Net.Http;
using WWebJsService;
using System.Diagnostics;
using System.Text;

namespace WWebJS.NET;

public enum WorkerStatus{
    Connected,
    Error,
    Connecting
}
public class WWebJSWorker:IDisposable
{
    static string GetEnvSpecificDefaultServerExecutablePath(){
        string folder = "x86";
        if(Environment.Is64BitProcess) folder = "x64";
        return @$"{folder}\wwebjs-dotnet-server.exe";
    }
    public static string ServerExecutablePath {get;set;} = GetEnvSpecificDefaultServerExecutablePath();

    private WWebJsService.WWebJsService.WWebJsServiceClient _Client; 
    public WWebJsService.WWebJsService.WWebJsServiceClient Client 
    {

        get{
            if(Status!=WorkerStatus.Connected) throw new InvalidOperationException($"Cannot use the worker instance, status='{Status}'");
            return _Client;
        }
        private set {_Client = value;}
    }
    public WWebJSWorker()
    {
        
        NamedPipeName = Guid.NewGuid().ToString();
        
        
    }

    public async Task Start(){

        try
        {
            if(!File.Exists(ServerExecutablePath)) throw new Exception($"executable not found: '{ServerExecutablePath}'");

            Status = WorkerStatus.Connecting;
            var processStarted = await StartWorkerWithArgs(new string[]{NamedPipeName});
        
            if(!processStarted) throw new Exception("cannot start process");
            

            var channel = new GrpcDotNetNamedPipes.NamedPipeChannel(".", NamedPipeName);
            Client = new WWebJsService.WWebJsService.WWebJsServiceClient(channel);
            if(await TryGetPingResponse()) Status = WorkerStatus.Connected;
            else throw new Exception("cannot start process, ping err");
        }
        catch (System.Exception err)
        {
            
            SetError(err);
            throw;
        }
        
    }

    async Task<bool> TryGetPingResponse(){
        return await Task.FromResult(true);
    }
    void SetError(Exception err){
        this.Status =  WorkerStatus.Error;
        this.Error=err;
    }
    public Exception Error{get;private set;}
    string NamedPipeName {get;set;}
    Process process;

    //never returns false, throws on errors
    private async Task<bool> StartWorkerWithArgs(string[] args)
{
    var exePath = ServerExecutablePath;
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

        DataReceivedEventHandler? onData = null;;
        DataReceivedEventHandler? onError = null;
        onData = (s,e)=>{
            if (e.Data != null && e.Data.Contains("Server listening"))
            {
                startupLogs.AppendLine(e.Data);
                process.OutputDataReceived-=onData;
                process.ErrorDataReceived-=onError;
                tcs.TrySetResult(true);
            }
        };
        onError = (s,e)=>{
            if (e.Data != null)
            {
                startupLogs.AppendLine(e.Data);
                Debug.WriteLine($"Worker Error: {e.Data}");
            }
        };
        process.OutputDataReceived += onData;
        process.ErrorDataReceived += onError;
        process.Exited += (sender, e) =>
        {
            if (!tcs.Task.IsCompleted)
            {
                process.OutputDataReceived-=onData;
                process.ErrorDataReceived-=onError;
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
        throw new Exception("cannot start process",err);
    }
}


    public void Dispose(){

        //Client.Dispose();
        if(process!=null)
        process.Dispose();


    }
    public void Close(){
        Dispose();
    }
    public WorkerStatus Status {get;set;}
    public static object hello(){
   
      return "v1";
   }
}




