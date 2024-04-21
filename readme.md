
# WWebJS.NET

A thin wrapper around [whatsapp-web.js](https://wwebjs.dev/) that uses Protocol Buffers RPC over named pipes. It aims to expose a similar API using C#.

## Example
```c#
//start the node server
var workerStartInfo = new WWebJSWorkerStartInfo(("x64/node.exe"), @"/wwebjs-dotnet-server/");
var worker = new WWebJSWorker(workerStartInfo));
await worker.Start(); 

//configure 
await worker.Channel.SetOptionsAsync(new WWebJSServiceOptions()
{
    ChromeExecutablePath = "path/to/chrome.exe", 
    HeadlessChromeMode = false,
    UserDataDir = @"/myAppData/sessions/",//root sessions data directory
    Verbose=true,
});

//create a Client 
var stream = worker.Channel.InitClient(new InitClientRequest()
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
        case ClientEventType.Message:
            if (current.DataJson["body"] == '!ping') 
            {
                var result = await worker.Channel.SendMessageAsync(new SendMessageRequest()
                    {
                        ChatId = current.DataJson["from"],
                        ClientHandle = "foo",
                        Content = new MessageContent() { Text="pong"},
                    });
                Console.WriteLine($"Message sent: ${result}")
            }
            break;
        case ClientEventType.MessageReceived:
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
