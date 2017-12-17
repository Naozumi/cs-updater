using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections;
using System.Reflection;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;
using NLog;

namespace cs_updater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        UpdateHash hashObject = new UpdateHash();
        HttpClient client = new HttpClient();
        String rootUrl = "";
        List<InstallPath> installDirs = new List<InstallPath>();
        InstallPath ActiveInstall = new InstallPath();
        List<News> news = new List<News>();
        Boolean allowWebNavigation = true;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public MainWindow()
        {

            InitializeComponent();

            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            if (Properties.Settings.Default.Dev == true)
            {
                DevMenu.Visibility = Visibility.Visible;
            }

            SetNews("Loading news...");
            LoadInstallDirs();
            LoadNews();
        }

        private void LoadInstallDirs()
        {
            menuInstallDirs.Items.Clear();
            try
            {
                installDirs = JsonConvert.DeserializeObject<List<InstallPath>>(Properties.Settings.Default.installDirs);
            }
            catch
            {
                Properties.Settings.Default.installDirs = null;
            }
            foreach (InstallPath install in installDirs)
            {
                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem
                {
                    Header = install.Name,
                };
                mi.Click += new RoutedEventHandler(InstallDir_Click);
                mi.IsCheckable = true;
                mi.Tag = install;
                if (install.IsDefault)
                {
                    ActiveInstall = install;
                    mi.IsChecked = true;
                }
                menuInstallDirs.Items.Add(mi);
            }

            //Add the extra options at bottom
            menuInstallDirs.Items.Add(new Separator());
            System.Windows.Controls.MenuItem miAdd = new System.Windows.Controls.MenuItem
            {
                Header = "Edit paths..."
            };
            miAdd.Click += new RoutedEventHandler(OpenPathEditor);
            miAdd.IsCheckable = false;
            menuInstallDirs.Items.Add(miAdd);

            //System.Windows.Controls.MenuItem miAuto = new System.Windows.Controls.MenuItem
            //{
            //    Header = "Automatically locate installations..."
            //};
            //miAuto.Click += new RoutedEventHandler(AutomaticallyAddInstalls);
            //miAuto.IsCheckable = false;
            //menuInstallDirs.Items.Add(miAuto);
        }

        private void InstallDir_Click(Object sender, RoutedEventArgs e)
        {
            foreach (Object item in menuInstallDirs.Items)
            {
                if (item.GetType() == typeof(System.Windows.Controls.MenuItem))
                {
                    System.Windows.Controls.MenuItem i = (System.Windows.Controls.MenuItem)item;
                    i.IsChecked = false;
                }
            }
            System.Windows.Controls.MenuItem mi = sender as System.Windows.Controls.MenuItem;
            ActiveInstall = (InstallPath)mi.Tag;
            mi.IsChecked = true;
        }

        private void OpenPathEditor(Object sender, RoutedEventArgs e)
        {
            InstallPathWindow ipw = new InstallPathWindow(installDirs);
            if ((bool)ipw.ShowDialog())
            {
                Properties.Settings.Default.installDirs = JsonConvert.SerializeObject(installDirs);
                Properties.Settings.Default.Save();
            }
            LoadInstallDirs();
        }

        

        public async void LoadNews()
        {
            String newsString = await Task.Run(() => Download_JSON_File(Properties.Settings.Default.newsUrl));

            if (newsString != null && (newsString.StartsWith("[") || newsString.StartsWith("{")))
            {
                news = await Task.Run(() => JsonConvert.DeserializeObject<List<News>>(newsString));
                foreach (var item in news)
                {
                    list_news.Items.Add(item.subject);
                }
                SetNews(news[0].message);
            }
        }

        private void SetNews(string body)
        {
            allowWebNavigation = true;
            Web_News.NavigateToString("<html><head><style>html{background-color:'#fff'; font-family: Tahoma, Verdana, Arial, Sans-Serif; font-size: 14px;} a:link {color: #d97b33;" +
                "text-decoration: none;}a:visited{color:#d97b33;text-decoration:none;}a:hover,a:active{color: #886203;text-decoration: underline;}img{border:none}</style>" +
                "</head><body oncontextmenu='return false; '>" + body + "</body></html>");
        }

        private void Web_News_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
        {
            // first page needs to be loaded in webBrowser control
            if (allowWebNavigation)
            {
                allowWebNavigation = false;
                return;
            }

            // cancel navigation to the clicked link in the webBrowser control
            e.Cancel = true;

            var startInfo = new ProcessStartInfo
            {
                FileName = e.Uri.ToString()
            };

            Process.Start(startInfo);
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
            SetNews("<pre>" + jobjString + "</pre>");
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
                    if (f.Files != null) CompressFiles(outputDirectory, subDirectory + f.Name + "\\", f.Files);
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
            btn_update.Content = "Updating...";
            progressBar.Value = 0;
            progressBarText.Content = "Starting download...";
            var failed = false;

            try
            {
                if (ActiveInstall.Path == "")
                {
                    throw new Exception("Install directory not set.");
                }
                else if (!Directory.Exists(ActiveInstall.Path))
                {
                    throw new Exception("Install directory " + ActiveInstall.Path + " does not exist.");
                }

                //Download JSON and decide on best host
                List<HostServer> hosts = new List<HostServer>();

                String[] hostUrls = Properties.Settings.Default.urls.Split(',');
                foreach (var url in hostUrls)
                {
                    hosts.Add(await Task.Run(() => VerifyHostServer(url, ActiveInstall.Password)));
                }

                HostServer master = new HostServer
                {
                    Json = new UpdateHash
                    {
                        ModuleVersion = "0.0.0"
                    }
                };
                foreach (HostServer host in hosts)
                {
                    if (host.Working)
                    {
                        if (Version.Parse(host.Json.ModuleVersion) > Version.Parse(master.Json.ModuleVersion))
                        {
                            master = host;
                        }
                        else if (Version.Parse(host.Json.ModuleVersion) == Version.Parse(master.Json.ModuleVersion))
                        {
                            if (host.Time > master.Time)
                            {
                                master = host;
                            }
                        }
                    }
                }
                if (!master.Working)
                {
                    foreach (HostServer host in hosts)
                    {
                        if (host.InvalidPassword)
                        {
                            throw new Exception("401");
                        }
                    }
                    logger.Error("Unable to connect to download servers.");
                    throw new Exception("Unable to connect to download servers.");
                }

                rootUrl = master.Url;
                if (!rootUrl.EndsWith("/")) rootUrl += "/";
                hashObject = master.Json;
                var t = await Task.Run(() => Update_Game_Files());
            }
            catch (Exception ex)
            {
                //log all errors, but only display the first fatal error to the user (threads can terminate after initial throw).
                logger.Error(ex);
                if (!failed)
                {
                    failed = true;
                    if (ex.Message == "401")
                    {
                        System.Windows.Forms.MessageBox.Show("Unabled to download update.\n\nBeta password is incorrect.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progressBarText.Content = "Incorrect beta password";
                        progressBar.Value = 0;
                    }
                    else
                    {
                        if (ex.InnerException != null)
                        {
                            System.Windows.Forms.MessageBox.Show("Unabled to download update. \n\n" + ex.InnerException.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else
                        {
                            System.Windows.Forms.MessageBox.Show("Unabled to download update. \n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        progressBarText.Content = "Error downloading files";
                        progressBar.Value = 0;
                    }
                }
            }
            if (!failed) progressBarText.Content = "Current version: " + hashObject.ModuleVersion + " - Ready to play";
            btn_update.Content = "Update";
            btn_update.IsEnabled = true;
        }

        private async Task<Boolean> Update_Game_Files()
        {
            Queue pending = new Queue(hashObject.getFiles());
            List<Task<UpdateHashItem>> working = new List<Task<UpdateHashItem>>();
            float count = pending.Count;
            var errors = 0;

            hashObject.Source = ActiveInstall.Path;

            foreach (UpdateHashItem f in hashObject.getFolders())
            {
                System.IO.Directory.CreateDirectory(ActiveInstall.Path + f.Path);
            }

            while (pending.Count + working.Count != 0 && errors < 30)
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
            if (File.Exists(hashObject.Source + item.Path))
            {
                if (item.Attempts > 3) throw new Exception("Unable to verify file: " + item.Path);
                using (FileStream stream = new FileStream(ActiveInstall.Path + item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
                        item.Attempts++;
                    }
                }
            }

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
                    using (FileStream fileStream = new FileStream(ActiveInstall.Path + item.Path, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (Stream stream = await response.Content.ReadAsStreamAsync())
                    using (GZipStream decompressedStream = new GZipStream(stream, CompressionMode.Decompress))
                    {
                        decompressedStream.CopyTo(fileStream);
                    }

                    using (FileStream stream = new FileStream(ActiveInstall.Path + item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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

        private async Task<HostServer> VerifyHostServer(String url, String password)
        {
            var hostinfo = new HostServer
            {
                Working = false
            };

            if (!url.EndsWith("/")) url += "/";
            hostinfo.Url = url;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            string filename = "";
            var jsonString = "";
            try
            {
                if (password == "" || password == null)
                {
                    filename = "cs.json";
                }
                else
                {
                    var jsonStringBeta = (await Task.Run(() => Download_Beta_JSON_File(url, password)));
                    var betaInfo = JsonConvert.DeserializeObject<BetaInfo>(jsonStringBeta);
                    filename = betaInfo.filename;
                }
                jsonString = (await Task.Run(() => Download_JSON_File(url + filename)));
            }
            catch (Exception ex)
            {
                watch.Stop();
                hostinfo.Time = watch.ElapsedMilliseconds;
                if (ex.Message == "401") hostinfo.InvalidPassword = true;
                hostinfo.DownloadException = ex;
                return hostinfo;
            }
            watch.Stop();
            hostinfo.Time = watch.ElapsedMilliseconds;

            if (jsonString != null && jsonString.StartsWith("{"))
            {
                try
                {
                    hostinfo.Json = await Task.Run(() => JsonConvert.DeserializeObject<UpdateHash>(jsonString));
                    hostinfo.Working = true;
                }
                catch (Exception ex)
                {
                    logger.Info(url);
                    logger.Error(ex);
                    hostinfo.DownloadException = ex;
                }
            }
            return hostinfo;
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
            catch (Exception ex)
            {
                if (count <= 1)
                {
                    Exception e = new Exception("Could not download json from: " + url, ex);
                    logger.Error(e);
                    return null;
                }
                var s = Download_JSON_File(url, count - 1);
                return null;
            }
        }

        /// <summary>
        /// Downloads the requested JSON file. Attempts 3 times then will throw and exception
        /// </summary>
        /// <param name="url">File to download</param>
        /// <param name="password">Password for the beta download</param>
        /// <param name="count">Number of times to try</param>
        /// <returns></returns>
        private async Task<String> Download_Beta_JSON_File(string url, String password, int count = 3)
        {
            try
            {
                Dictionary<string, string> pairs = new Dictionary<string, string>();
                pairs.Add("password", password);
                FormUrlEncodedContent formContent = new FormUrlEncodedContent(pairs);


                HttpResponseMessage response = await client.PostAsync(url + "beta-check.ajax.php", formContent);
                response.EnsureSuccessStatusCode();
                return response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("401"))
                {
                    Exception e = new Exception("Beta password is incorrect.", ex);
                    logger.Error(e);
                    throw new Exception("401");
                }
                else if (count <= 1)
                {
                    Exception e = new Exception("Could not download json from: " + url, ex);
                    logger.Error(e);
                    return null;
                }
                var s = Download_JSON_File(url, count - 1);
                return null;
            }
        }

        private string GetPublishedVersion()
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            return string.Format("Product Name: {4}, Version: {0}.{1}.{2}.{3}", ver.Major, ver.Minor, ver.Build, ver.Revision, Assembly.GetEntryAssembly().GetName().Name);
        }

        static string ProgramFilesx86()
        {
            if (8 == IntPtr.Size
                || (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"))))
            {
                return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            }

            return Environment.GetEnvironmentVariable("ProgramFiles");
        }

        private void list_news_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (list_news.SelectedIndex > -1 && list_news.SelectedIndex < news.Count)
            {
                SetNews(news[list_news.SelectedIndex].message);
            }
        }
    }
}

