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
            Task.Run(async () => await Update_Game_Files());
        }

        private async Task<Boolean> Update_Game_Files()
        {

            Queue pending = new Queue(hashObject.getFiles());
            List<Task<UpdateHashItem>> working = new List<Task<UpdateHashItem>>();
            float count = pending.Count;
            var errors = 0;

            hashObject.Source = installPath;

            foreach (UpdateHashItem f in hashObject.getFolders())
            {
                System.IO.Directory.CreateDirectory(installPath + f.Path);
            }

            while (pending.Count + working.Count != 0 && errors < 30)
            {
                if (working.Count < 4 && pending.Count != 0)
                {
                    var item = (UpdateHashItem)pending.Dequeue();
                    if (item.Verified == false)
                    {
                        working.Add(Task.Run(async () => await Update_Item(item)));
                    }
                }
                else
                {
                    Task<UpdateHashItem> t = await Task.WhenAny(working);
                    working.RemoveAll(x => x.IsCompleted);
                    if (t.Result.Verified)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            progressBarText.Content = "Current version: " + hashObject.ModuleVersion + " - " + (count - pending.Count) + " / " + count;
                            progressBar.Value = ((count - pending.Count) / count) * 100;
                        });
                    }
                    else
                    {
                        errors++;
                        pending.Enqueue(t.Result);
                    }
                }
            }
            if (errors >= 30)
            {
                throw new Exception("Unable to download files from server. Please contact NI Support.");
            }
            return true;
        }

        private async Task<UpdateHashItem> Update_Item(UpdateHashItem item)
        {

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(rootUrl + "files/" + item.Crc + ".gz");
            }
            catch (Exception ex)
            {
                //web server sent an error message
                logger.Error(new Exception("Error downloading the file: " + rootUrl + "files/" + item.Crc + ".gz" + "  " + item.Path, ex));
                item.Attempts++;
                if (item.Attempts > 3) throw;
                return item;
            }


            if (response.IsSuccessStatusCode)
            {
                try
                {
                    using (FileStream fileStream = new FileStream(installPath + item.Path, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (Stream stream = await response.Content.ReadAsStreamAsync())
                    using (GZipStream decompressedStream = new GZipStream(stream, CompressionMode.Decompress))
                    {
                        decompressedStream.CopyTo(fileStream);
                    }

                    using (FileStream stream = new FileStream(installPath + item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (SHA1Managed sha = new SHA1Managed())
                    {
                        byte[] checksum = sha.ComputeHash(stream);
                        if (BitConverter.ToString(checksum).Replace("-", string.Empty).ToLower() == item.Crc)
                        {
                            item.Verified = true;
                            return item;
                        }
                        else
                        {
                            return item;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // something odd went wrong
                    logger.Error(new Exception("Error verifying the file: " + item.Name + "  " + item.Path, ex));
                    if (item.Attempts >= 2) throw new Exception("Error:" + ex.InnerException.Message);
                    item.Attempts++;
                    return item;
                }

            }
            else
            {
                // server said no
                logger.Error("Failed to download: " + item.Name +
                    Environment.NewLine + " Attempt: " + item.Attempts.ToString() +
                    Environment.NewLine + "Reason: " + response.ReasonPhrase +
                    Environment.NewLine + "Request: " + response.RequestMessage);
                if (item.Attempts >= 2) throw new Exception("Error Code: " + response.ReasonPhrase + "\nFile: " + item.Name);
                item.Attempts++;
                return item;
            }

        }
    }
}
