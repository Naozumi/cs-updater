using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
        HttpClient client = new HttpClient();
        String urlBase = "";
        String installBase = "";

        public MainWindow()
        {
            InitializeComponent();
            web_news.NavigateToString(BlankWebpage());
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
        }

        private void CompressFiles(string baseDirectory, string subDirectory, List<UpdateHashItem> files)
        {
            if (!baseDirectory.EndsWith("\\")) baseDirectory += "\\";
            if (subDirectory != null && !subDirectory.EndsWith("\\")) subDirectory += "\\";

            if (files == null) return;
            foreach (var f in files)
            {
                if (f.isFolder())
                {
                    Directory.CreateDirectory(baseDirectory + subDirectory + f.Name);
                    if (f.Files != null) CompressFiles(baseDirectory, subDirectory + f.Name + "\\", f.Files);
                }
                else
                {
                    CreateTarGZ(baseDirectory + subDirectory + f.Name, hashObject.Source + "\\" + subDirectory + f.Name);
                }
            }
        }

        private void CreateTarGZ(string tgzFilename, string fileName)
        {
            using (var outStream = File.Create(tgzFilename + ".tgz"))
            using (var gzoStream = new GZipOutputStream(outStream))
            using (var tarArchive = TarArchive.CreateOutputTarArchive(gzoStream))
            {
                gzoStream.SetLevel(9);
                tarArchive.RootPath = System.IO.Path.GetDirectoryName(fileName);

                var tarEntry = TarEntry.CreateEntryFromFile(fileName);
                tarEntry.Name = System.IO.Path.GetFileName(fileName);

                tarArchive.WriteEntry(tarEntry, true);
            }
        }

        private async void Button_Update_Click(object sender, RoutedEventArgs e)
        {
            btn_update.IsEnabled = false;
            try
            {
                string jobjString = await Task.Run(() => Download_File_Return("https://nordinvasion.com/mod/cs.json"));
                hashObject = await Task.Run(() => JsonConvert.DeserializeObject<UpdateHash>(jobjString));
                web_news.NavigateToString("<html><head><style>html{background-color:'#fff'}</style></head><body oncontextmenu='return false; '>Verifying files: " + hashObject.ModuleVersion + "</body></html>");
                await Task.Run(() => verifyFiles());
                web_news.NavigateToString("<html><head><style>html{background-color:'#fff'}</style></head><body oncontextmenu='return false; '>Downloading version: " + hashObject.ModuleVersion + "</body></html>");
                var t = await Task.Run(() => Download_Game_Files());
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

        private async void verifyFiles()
        {
            Queue pending = new Queue(hashObject.getFiles());
            List<Task<UpdateHashItem>> working = new List<Task<UpdateHashItem>>();
            var i = 0.0;
            var count = pending.Count;
            installBase = "C:\\cstest\\ni\\";

            while (pending.Count + working.Count != 0)
            {
                if (working.Count < 4 && pending.Count != 0)
                {
                    var item = (UpdateHashItem)pending.Dequeue();
                    working.Add(Task.Run(async () => await verifyHash(item)));
                }
                else
                {
                    Task<UpdateHashItem> t = await Task.WhenAny(working);
                    working.RemoveAll(x => x.IsCompleted);
                    if (t.Result.Downloaded)
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
        }

        private async Task<Boolean> Download_Game_Files()
        {
            Queue pending = new Queue(hashObject.getFiles());
            List<Task<UpdateHashItem>> working = new List<Task<UpdateHashItem>>();
            var i = 0.0;
            var count = pending.Count;
            urlBase = "https://nordinvasion.com/mod/" + hashObject.ModuleVersion + "/";
            installBase = "C:\\cstest\\ni\\";

            foreach (UpdateHashItem f in hashObject.getFolders())
            {
                System.IO.Directory.CreateDirectory(installBase + f.Path);
            }

            while (pending.Count + working.Count != 0)
            {
                if (working.Count < 4 && pending.Count != 0)
                {
                    var item = (UpdateHashItem)pending.Dequeue();
                    working.Add(Task.Run(async () => await Download_File(item)));
                }
                else
                {
                    Task<UpdateHashItem> t = await Task.WhenAny(working);
                    working.RemoveAll(x => x.IsCompleted);
                    if (t.Result.Downloaded)
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

        private async Task<UpdateHashItem> Download_File(UpdateHashItem item)
        {
            HttpResponseMessage response = await client.GetAsync(urlBase + item.Path + ".gz");

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    using (FileStream fileStream = new FileStream(installBase + item.Path + ".gz", FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }
                    item.Downloaded = true;
                    return item;
                }
                catch (Exception ex)
                {
                    if (item.Attempts >= 2) throw new Exception("Error:" + ex.InnerException.Message);
                    item.Attempts++;
                    return item;
                }

            }
            else
            {
                if (item.Attempts >= 2) throw new Exception("Error Code: " + response.ReasonPhrase + "\nFile: " + item.Name);
                item.Attempts++;
                return item;
            }
        }

        private async Task<String> Download_File_Return(string url)
        {
            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            return response.Content.ReadAsStringAsync().Result;
        }

        private async Task<UpdateHashItem> verifyHash(UpdateHashItem item)
        {
            item.Verified = false;
            if (File.Exists(item.Path))
            {
                using (FileStream stream = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) // File.OpenRead(file.FullName))
                using (SHA1Managed sha = new SHA1Managed())
                {
                    byte[] checksum = sha.ComputeHash(stream);
                    if (BitConverter.ToString(checksum).Replace("-", string.Empty).ToLower() == item.Crc)
                    {
                        item.Verified = true;
                    }
                }
            }
            return item;
        }
    }
}
