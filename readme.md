
# WWebJS.NET

A thin wrapper around [whatsapp-web.js](https://wwebjs.dev/) that uses Protocol Buffers RPC over named pipes. It aims to expose a similar API using C#.
## Prerequisites
You'll need to install the node package ```wwebjs-dotnet-server``` which includes ```whatsapp-web.js``` and the required dependencies

### Install using npm:
```bat
cd C:\MyAppModules\
mkdir wds
cd wds
set PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true
npm init -y
npm install wwebjs-dotnet-server
```

or install at runtime using the `WWebJSHelper` class, which does the same:

```c#
//specify where you want the node dependencies installed:
WWebJSHelper.WdsParentProjectDirectory = @"C:\MyAppModules\wds";
//provide the full path to npm 
WWebJSHelper.NpmPath = @"C:\MyAppModules\npm.cmd"; 
//first install:
await WWebJSHelper.Install(true);
//checking for updates is supported:
string latestVersion = await WWebJSHelper.CheckUpdate();
if(latestVersion != WWebJSHelper.InstalledVersion)
{
    await WWebJSHelper.InstallUpdate(latestVersion);
}
```
## Usage
```c#
//assuming you have installed wwebjs-dotnet-server at the specified location
//start the node server
var workerStartInfo = new WWebJSWorkerStartInfo(("x64/node.exe"), @"C:/MyAppModules/wds/node_modules/wwebjs-dotnet-server/");
var worker = new WWebJSWorker(workerStartInfo));
await worker.Start(); 

//configure 
await worker.Proxy.SetOptionsAsync(new WWebJSServiceOptions()
{
    ChromeExecutablePath = "path/to/chrome.exe", 
    HeadlessChromeMode = false,
    UserDataDir = @"/myAppData/sessions/",//root sessions data directory
    Verbose=true,
});

//create a Client 
var stream = worker.Proxy.InitClient(new InitClientRequest()
{
    ClientCreationOptions = new ClientCreationOptions
    {
        SessionFolder = "foo" //Local auth directory: /myAppData/sessions/foo
    }
});

//Handling Client events
while (await stream.ResponseStream.MoveNext(CancellationToken.None))
{
    var current=stream.ResponseStream.Current;
    switch (current.EventType)
    {
        case ClientEventType.Ready:
            Console.WriteLine($"Client Ready'");
            break;
        case ClientEventType.MessageReceived:
            if (current.DataJson["body"] == '!ping') 
            {
                var result = await worker.Proxy.SendMessageAsync(new SendMessageRequest()
                    {
                        ChatId = current.DataJson["from"],
                        ClientHandle = "foo",
                        Content = new MessageContent() { Text="pong"},
                    });
                Console.WriteLine($"Message sent: ${result}")
            }
            break;
        case ClientEventType.MessageCreate:
            break;
        case ClientEventType.QrReceived:
            Console.WriteLine($"Qr: {current.DataJson}'");
            break;
        case ClientEventType.LoadingScreen:
            Console.WriteLine($"Loading Screen");
            break;
        case ClientEventType.Disconnected:
             Console.WriteLine($"Client Disconnected");
        default:
            break;
    }
}

```
