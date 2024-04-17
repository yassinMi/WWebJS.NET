using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
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
            button1.Click += async(s, e) => {

                try
                {
                    labelStatus.Content = "getting result...";
                    using (var worker = new WWebJSWorker())
                    {
                        await worker.Start();
                        var contact = new Contact() { Phone = "yass phone" };
                        var req = new GetLastMessageRequest() { Contact = contact};

                        var res = await worker.Client.GetLastMessageAsync(req);
                        labelStatus.Content = res.Message.Body;
                    }
                }
                catch (Exception err)
                {
                    labelStatus.Content =err.ToString();
                      
                }
                
                
            };
        }
    }
}
