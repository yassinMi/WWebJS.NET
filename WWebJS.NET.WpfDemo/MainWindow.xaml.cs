using System;
using System.Collections.Generic;
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

            LogInfo("demo app  started");

            button1.Click += async (s, e) =>
            {

                try
                {
                    bool packaged = ! Keyboard.IsKeyDown(Key.LeftCtrl);
                    if (!packaged)
                    {
                        //hello test using grpc directly
                        LogInfo("running test with existing worker instance (TEST pipe name]");
                        labelStatus.Content = "getting result ...";
                        var channel = new GrpcDotNetNamedPipes.NamedPipeChannel(".", "TEST");
                        var Client = new WWebJsService.WWebJsService.WWebJsServiceClient(channel);
                        var contact = new Contact() { Phone = "yass phone" };
                        var req = new GetLastMessageRequest() { Contact = contact };
                        var res = await Client.GetLastMessageAsync(req);
                        labelStatus.Content = res.Message.Body;
                    }
                    else
                    {
                        //hello test

                        LogInfo("starting packaged worker ...");
                        WWebJSWorker.ServerExecutablePath = @"E:\TOOLS\WWebJS.NET\WWebJS.NET\wwebjs-dotnet-server\bin\wwebjs-dotnet-server.exe";
                        using (var worker = new WWebJSWorker())
                        {
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
                            labelStatus.Content = res.Message.Body;
                        }
                    }

                    
                }
                catch (Exception err)
                {
                    labelStatus.Content = err.ToString();

                }


            };
        }
        private void LogInfo(string str)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                logsRtb.Document.Blocks.Add(new Paragraph(new Run(str) { }));
            });

        }
        private void LogError(string str)
        {
            App.Current.Dispatcher.Invoke(() =>
            {

                logsRtb.Document.Blocks.Add(new Paragraph(new Run(str) { Foreground = Brushes.Red }));
            });


        }
    }
}
