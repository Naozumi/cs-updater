using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections;
using NLog;
using cs_updater_lib;
using System.IO.Pipes;

namespace cs_updater_installer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class UpdateWindow : Window
    {
        private UpdateHash hashObject;
        private string installPath;
        private HttpClient client = new HttpClient();
        private String rootUrl = "";
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public UpdateWindow()
        {
            InitializeComponent();
            this.Show();

            //CRASHES!!!!!!! :(

            String[] arguments = System.Environment.GetCommandLineArgs();

            using (PipeStream pipeClient = new AnonymousPipeClientStream(PipeDirection.In, arguments[0]))
            {

                Console.WriteLine("[CLIENT] Current TransmissionMode: {0}.",
                   pipeClient.TransmissionMode);

                using (StreamReader sr = new StreamReader(pipeClient))
                {
                    // Display the read text to the console
                    string temp;

                    // Wait for 'sync message' from the server.
                    do
                    {
                        Console.WriteLine("[CLIENT] Wait for sync...");
                        temp = sr.ReadLine();
                    }
                    while (!temp.StartsWith("SYNC"));

                    // Read the server data and echo to the console.
                    while ((temp = sr.ReadLine()) != null)
                    {
                        progressBarText.Content += temp;
                    }
                }
            }
        }

        public UpdateWindow(UpdateHash updateHashObject, string path, string url)
        {
            InitializeComponent();
            hashObject = updateHashObject;
            installPath = path;
            rootUrl = url;
        }

        public void doUpdate()
        {
            this.Show();
            this.Activate();
        }

        
    }
}
