using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Forms;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Collections;

namespace cs_updater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        UpdateHash hashObject = new UpdateHash();
        HttpClient client;
        String urlBase = "";
        String installBase = "";

        public MainWindow()
        {
            InitializeComponent();
            web_news.NavigateToString(BlankWebpage());
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            client = new HttpClient(handler);
        }

        private string BlankWebpage()
        {
            return "<html><head><style>html{background-color:'#fff'}</style></head><body oncontextmenu='return false; '></body></html>";
        }

        private UpdateHash BuildStructure(DirectoryInfo directory)
        {
            var hash = new UpdateHash
            {
                Module = "NordInvasion",
                Files = BuildFileStructure(directory)
            };

            return hash;
        }

        private static List<UpdateHashItem> BuildFileStructure(DirectoryInfo directory)
        {
            var jsonObject = new List<UpdateHashItem>();

            try
            {
                foreach (var file in directory.GetFiles())
                {
                    var crc = string.Empty;
                    {
                        using (FileStream stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) // File.OpenRead(file.FullName))
                        {
                            using (SHA1Managed sha = new SHA1Managed())
                            {
                                byte[] checksum = sha.ComputeHash(stream);
                                crc = BitConverter.ToString(checksum)
                                    .Replace("-", string.Empty).ToLower();
                            }
                        }
                        jsonObject.Add(new UpdateHashItem(file.Name, crc));
                    }
                }
            }
            catch (Exception err)
            {
                System.Windows.Forms.MessageBox.Show("Unable to complete building of JSON file due to error: \n\n" + err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            foreach (var folder in directory.GetDirectories())
            {
                jsonObject.Add(new UpdateHashItem(folder.Name, BuildFileStructure(new DirectoryInfo(folder.FullName))));
            }

            return jsonObject;
        }

        private async void Modules_Build_Click(object sender, RoutedEventArgs e)
        {
            //note: currently this button just does "useful" features required in/to prove the final version.

            //set the directory
            DirectoryInfo dir = null;

            CommonOpenFileDialog dialog = new CommonOpenFileDialog
            {
                InitialDirectory = "C:\\",
                IsFolderPicker = true
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                dir = new DirectoryInfo(dialog.FileName);
            }

            if (dir == null) return;

            //build the json Object which contains all the files and folders
            hashObject = await Task.Run(() => BuildStructure(dir));
            hashObject.Source = dir.FullName;
            if (hashObject.Files == null) return;

            //convert the json object to a json string
            string jobjString = await Task.Run(() => JsonConvert.SerializeObject(hashObject, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));

            //display the json file in the web browser window.
            string output = "<html><body oncontextmenu='return false; '><pre>" + jobjString + "</pre></body</html>";
            web_news.NavigateToString(output);
        }

        private void Modules_Child_Count_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.MessageBox.Show("Files & Folders found: " + hashObject.getFileCount().ToString(), "F&F Count");
        }

        private void Modules_Compress_Click(object sender, RoutedEventArgs e)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            if (hashObject.Source == null) return;

            //set the directory
            DirectoryInfo dir = null;

            CommonOpenFileDialog dialog = new CommonOpenFileDialog
            {
                InitialDirectory = "C:\\",
                IsFolderPicker = true
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                dir = new DirectoryInfo(dialog.FileName);
            }

            if (dir == null) return;

            CompressFiles(dir.FullName, null, hashObject.Files);
            watch.Stop();
            System.Windows.Forms.MessageBox.Show("Done - " + watch.ElapsedMilliseconds.ToString(), "Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CompressFiles(string outputDirectory, string subDirectory, List<UpdateHashItem> files)
        {
            if (!outputDirectory.EndsWith("\\")) outputDirectory += "\\";
            if (subDirectory != null && !subDirectory.EndsWith("\\")) subDirectory += "\\";

            if (files == null) return;
            foreach (var f in files)
            {
                if (f.isFolder())
                {
                    Directory.CreateDirectory(outputDirectory + subDirectory + f.Name);
                    if (f.Files != null) CompressFiles(outputDirectory, subDirectory + f.Name + "\\", f.Files, tgz);
                }
                else
                {
                    CreateGz(hashObject.Source + "\\" + subDirectory + f.Name, outputDirectory + subDirectory + f.Name);
                }
            }
        }


        private void CreateGz(String inputFile, String outputFile)
        {
            using (FileStream originalFileStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (FileStream compressedFileStream = File.Create(outputFile + ".gz"))
            using (GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress))
            {
                originalFileStream.CopyTo(compressionStream);
            }
        }

        private async void Button_Update_Click(object sender, RoutedEventArgs e)
        {
            btn_update.IsEnabled = false;


            try
            {
                string jobjString = await Task.Run(() => Download_JSON_File("https://nordinvasion.com/mod/cs.json"));
                hashObject = await Task.Run(() => JsonConvert.DeserializeObject<UpdateHash>(jobjString));
                web_news.NavigateToString("<html><head><style>html{background-color:'#fff'}</style></head><body oncontextmenu='return false; '>Downloading version: " + hashObject.ModuleVersion + "</body></html>");
                var t = await Task.Run(() => Update_Game_Files());
                web_news.NavigateToString("<html><head><style>html{background-color:'#fff'}</style></head><body oncontextmenu='return false; '>Download completed</body></html>");
            }
            catch (Exception ex)
            {
                if (ex.InnerException.Message != null)
                {
                    System.Windows.Forms.MessageBox.Show("Unabled to download update.: \n\n" + ex.InnerException.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("Unabled to download update.: \n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            }
            btn_update.IsEnabled = true;
        }

        private async Task<Boolean> Update_Game_Files()
        {
            Queue pending = new Queue(hashObject.getFiles());
            List<Task<UpdateHashItem>> working = new List<Task<UpdateHashItem>>();
            var i = 0.0;
            var count = pending.Count;
            urlBase = "https://nordinvasion.com/mod/" + hashObject.ModuleVersion + "/";
            //installBase = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\MountBlade Warband\\Modules\\NordInvasion2\\";
            installBase = "C:\\cstest5\\";
            hashObject.Source = installBase;

            foreach (UpdateHashItem f in hashObject.getFolders())
            {
                System.IO.Directory.CreateDirectory(installBase + f.Path);
            }

            while (pending.Count + working.Count != 0)
            {
                if (working.Count < 4 && pending.Count != 0)
                {
                    var item = (UpdateHashItem)pending.Dequeue();
                    working.Add(Task.Run(async () => await Update_Item(item)));
                }
                else
                {
                    Task<UpdateHashItem> t = await Task.WhenAny(working);
                    working.RemoveAll(x => x.IsCompleted);
                    if (t.Result.Verified)
                    {
                        i++;
                        this.Dispatcher.Invoke(() =>
                        {
                            progressBar.Value = (i / count) * 100;
                        });
                    }
                    else
                    {
                        pending.Enqueue(t.Result);
                        var x = i;
                    }
                }
            }
            return true;
        }

        private async Task<UpdateHashItem> Update_Item(UpdateHashItem item)
        {
            if (File.Exists(hashObject.Source + item.Path))
            {
                using (FileStream stream = new FileStream(installBase + item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
            else
            {
                HttpResponseMessage response;
                try
                {
                    response = await client.GetAsync(urlBase + item.Path + ".gz");
                }
                catch (Exception ex)
                {
                    //web server fucked up
                    item.Attempts++;
                    if (item.Attempts > 10) throw;
                    return item;
                }


                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        using (FileStream fileStream = new FileStream(installBase + item.Path, FileMode.Create, FileAccess.Write, FileShare.None))
                        using (Stream stream = await response.Content.ReadAsStreamAsync())
                        using (GZipStream decompressedStream = new GZipStream(stream, CompressionMode.Decompress))
                        {
                            decompressedStream.CopyTo(fileStream);
                        }

                        using (FileStream stream = new FileStream(installBase + item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
                        if (item.Attempts >= 2) throw new Exception("Error:" + ex.InnerException.Message);
                        item.Attempts++;
                        return item;
                    }

                }
                else
                {
                    // server said no
                    if (item.Attempts >= 2) throw new Exception("Error Code: " + response.ReasonPhrase + "\nFile: " + item.Name);
                    item.Attempts++;
                    return item;
                }
            }
        }

        /// <summary>
        /// Downloads the requested JSON file. Attempts 3 times then will throw and exception
        /// </summary>
        /// <param name="url">File to download</param>
        /// <param name="count">Number of times to try</param>
        /// <returns></returns>
        private async Task<String> Download_JSON_File(string url, int count = 3)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return response.Content.ReadAsStringAsync().Result;
            }
            catch
            {
                if (count <= 1) return null;
                var s = Download_JSON_File(url, count--);
                if (s.Result == null) throw;
                return null;
            }
        }
    }
}
