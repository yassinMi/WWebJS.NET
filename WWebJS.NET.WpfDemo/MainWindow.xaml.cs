using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
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

            button1.Click += async (s, e) =>
            {

                try
                {
                    bool packaged = ! Keyboard.IsKeyDown(Key.LeftCtrl);
                    if (!packaged)
                    {
                        //hello test using grpc directly
                        LogInfo("running test with existing worker instance (TEST pipe name]");
                        LogInfo($"getting result");
                        var channel = new GrpcDotNetNamedPipes.NamedPipeChannel(".", "TEST");
                        var Client = new WWebJsService.WWebJsService.WWebJsServiceClient(channel);
                        var contact = new Contact() { Phone = "yass phone" };
                        var req = new GetLastMessageRequest() { Contact = contact };
                        var res = await Client.GetLastMessageAsync(req);
                        LogInfo($"recieved response: {res.Message.Body}");
                    }
                    else
                    {
                        //hello test

                        LogInfo("starting packaged/node worker ...");
                        WWebJSWorkerStartInfo wsi = new WWebJSWorkerStartInfo(("x64/node.exe"), "wwebjs-dotnet-server/");

                        //WWebJSWorker.ServerExecutablePath = @"E:\TOOLS\WWebJS.NET\WWebJS.NET\wwebjs-dotnet-server\bin\wwebjs-dotnet-server.exe";
                        using (var worker =currentWorker = new WWebJSWorker(wsi))
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

                            await worker.Start();

                            var contact = new Contact() { Phone = "yass phone" };
                            var req = new GetLastMessageRequest() { Contact = contact };

                            var res = await worker.Client.GetLastMessageAsync(req);
                            LogInfo($"recieved response: {res.Message.Body}" );
                            await Task.Delay(1000);
                        }
                        LogInfo($"end scope:");

                    }


                }
                catch (Exception err)
                {
                    LogError( err.ToString());

                }


            };
        }
        WWebJSWorker currentWorker;
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
    }
}
