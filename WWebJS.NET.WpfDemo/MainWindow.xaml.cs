using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WWebJsService;

namespace WWebJS.NET.WpfDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            LogInfo("demo app started");

         
        }
        static WWebJSWorkerStartInfo wsi = new WWebJSWorkerStartInfo(("x64/node.exe"), @"E:\TOOLS\WWebJS.NET\WWebJS.NET\wwebjs-dotnet-server\");

        WWebJSWorker currentWorker;

        public WWebJSWorker CurrentLastingWorker { get; private set; }
        public string CurrentInitializedClientHandle { get; private set; }

        private void onWorkerStatusChanged(object sender, EventArgs e)
        {
            onStatus($"Worker Status: {currentWorker.Status}");
        }

        private void LogInfo(string str)
        {
            Debug.WriteLine(str);
            App.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                bool shouldScroll = logsRtb.VerticalOffset >= (logsRtb.ExtentHeight- logsRtb.ViewportHeight);

                //MessageBox.Show($"{logsRtb.VerticalOffset} > = {logsRtb.ViewportHeight} {logsRtb.ExtentHeight} {logsRtb.Document.PageHeight} {logsRtb.ActualHeight}");
                logsRtb.Document.Blocks.Add(new Paragraph(new Run(str) { }));
                if (shouldScroll) logsRtb.ScrollToEnd();
            }));

        }
        void onStatus(string status)
        {
            App.Current.Dispatcher.BeginInvoke(new Action(() => { labelStatus.Content = status; }));

        }
        private void LogError(string str)
        {
            Debug.WriteLine(str);

            App.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                bool shouldScroll = logsRtb.VerticalOffset >= (logsRtb.ExtentHeight - logsRtb.ViewportHeight);

                logsRtb.Document.Blocks.Add(new Paragraph(new Run(str) { Foreground = Brushes.Red }));
                if (shouldScroll) logsRtb.ScrollToEnd();
            }));


        }

       

        private void getChatsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                throw new NotImplementedException();
            }
            catch (Exception err)
            {
                repErr(err);
            }
        }

        private void getLastNMessagesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                throw new NotImplementedException();
            }
            catch (Exception err)
            {
                repErr(err);
            }
        }

        private async void sendToNumberButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sendToNumberRadioButton.IsChecked==true)
                {
                    var num = sendToNumberTb.Text;
                    if (string.IsNullOrWhiteSpace(num)) throw new InvalidUserOperation("eempty number");
                    if (CurrentLastingWorker == null) throw new InvalidUserOperation("current worker not initialized");
                    if (CurrentLastingWorker.Status != WorkerStatus.Connected) throw new InvalidUserOperation("current worker not connected");
                    if (CurrentInitializedClientHandle == null) throw new InvalidUserOperation("create a client first");

                    var res = await CurrentLastingWorker.Client.SendMessageAsync(new SendMessageRequest()
                    {
                        ChatId = num,
                        ClientHandle = CurrentInitializedClientHandle,
                        Content = new MessageContent() { Text="hi"},
                        
                    });
                    LogInfo($"wp message sent: {res.Message.Body}");

                }
                else
                {
                    if (chatsListBox.SelectedItem == null)
                    {
                        throw new InvalidUserOperation("no selected item");
                    }

                }
            }
            catch (Exception err)
            {
                repErr(err);
            }
        }
        class InvalidUserOperation: Exception
        {
            public InvalidUserOperation(string message):base(message)
            {

            }
            
        }

        private void repErr(Exception err)
        {
            string content = err.ToString();
            if (err is InvalidUserOperation) content = err.Message;
            LogError(content);
            MessageBox.Show(content, "WWebJS.NET.WpfDemo", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async void helloButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool packaged = !Keyboard.IsKeyDown(Key.LeftCtrl);
                if (!packaged)
                {
                    //hello test using grpc directly
                    LogInfo("running test with existing worker instance (TEST pipe name]");
                    LogInfo($"getting result");
                    var channel = new GrpcDotNetNamedPipes.NamedPipeChannel(".", "TEST");
                    var Client = new WWebJsService.WWebJsService.WWebJsServiceClient(channel);
                    var contact = new Contact() { Number = "yass phone" };
                    var req = new DevGetLastMessageRequest() { Contact = contact };
                    var res = await Client.DevGetLastMessageAsync(req);
                    LogInfo($"recieved response: {res.Message.Body}");
                }
                else
                {
                    //hello test

                    LogInfo("starting packaged/node worker ...");

                    //WWebJSWorker.ServerExecutablePath = @"E:\TOOLS\WWebJS.NET\WWebJS.NET\wwebjs-dotnet-server\bin\wwebjs-dotnet-server.exe";
                    using (var worker = currentWorker = new WWebJSWorker(wsi))
                    {
                        trackWorker(worker);

                        await worker.Start();

                        var contact = new Contact() { Number = "yass phone" };
                        var req = new DevGetLastMessageRequest() { Contact = contact };

                        var res = await worker.Client.DevGetLastMessageAsync(req);
                        LogInfo($"recieved response: {res.Message.Body}");
                        await Task.Delay(1000);
                    }
                    LogInfo($"end scope:");

                }


            }
            catch (Exception err)
            {
                LogError(err.ToString());

            }

        }
        void trackWorker(WWebJSWorker worker)
        {
            worker.StatusChanged += onWorkerStatusChanged;
            worker.ProcessOutputDataReceived += (ss, ee) =>
            {
                LogInfo(ee.Data);
            };
            worker.ProcessErrorDataReceived += (ss, ee) =>
            {
                LogError(ee.Data);
            };
            worker.Log = (ee) =>
            {
                LogInfo(ee);
            };
        }
        static string TestDataRoot = @"E:\TOOLS\WWebJS.NET\WWebJS.NET\data.yass";
        static string TestChromePath = @"C:\Program Files\WhatsappImagesScraper\chromium\win64-982053\chrome-win\chrome.exe";
        private async void createClientButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var clientHandle = clientHandleTb.Text;
                if (string.IsNullOrWhiteSpace(clientHandle)) throw new InvalidUserOperation("empty client name");
                if (!Regex.IsMatch(clientHandle, "[a-zA-Z0-9]+")) throw new InvalidUserOperation("invalid client name");


                    CurrentLastingWorker = new WWebJSWorker(wsi);
                trackWorker(CurrentLastingWorker);
                await CurrentLastingWorker.Start();
                //# set options
                await CurrentLastingWorker.Client.SetOptionsAsync(new WWebJSServiceOptions()
                {
                    ChromeExecutablePath = TestChromePath,
                    HeadlessChromeMode = false,
                    UserDataDir = TestDataRoot,
                    Verbose=true,
                });
                var stream = CurrentLastingWorker.Client.InitClient(new InitClientRequest()
                {
                    ClientCreationOptions = new ClientCreationOptions
                    {
                        SessionFolder = clientHandle
                    }
                });
                while (await stream.ResponseStream.MoveNext(CancellationToken.None))
                {
                    var curr = stream.ResponseStream.Current;
                    LogInfo($"[{curr.EventType}]: {curr.EventArgsJson} ");
                    switch (curr.EventType)
                    {
                        case ClientEventType.Authenticated:
                            break;
                        case ClientEventType.AuthenticationFailure:
                            break;
                        case ClientEventType.Ready:
                            break;
                        case ClientEventType.MessageReceived:
                            break;
                        case ClientEventType.MessageCreate:
                            break;
                        case ClientEventType.QrReceived:
                            break;
                        case ClientEventType.LoadingScreen:
                            break;
                        case ClientEventType.Disconnected:
                       
                        default:
                            break;
                    }
                }

            }
            catch (Exception err)
            {
                repErr(err);
            }

        }
    }
}
