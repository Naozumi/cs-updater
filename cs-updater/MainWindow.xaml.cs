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
using System.Diagnostics;
using NLog;
using System.Windows.Input;
using cs_updater_lib;
using System.IO.Pipes;
using System.Windows.Threading;

namespace cs_updater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private UpdateHash hashObject = new UpdateHash();
        private HttpClient client = new HttpClient();
        private String rootUrl = "";
        private List<InstallPath> installDirs = new List<InstallPath>();
        private InstallPath ActiveInstall = new InstallPath();
        private List<News> news = new List<News>();
        private Boolean allowWebNavigation = true;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private bool updateRequired = false;
        private bool filesVerified = false;

        private double progress = 0;
        private string progressText = "Loading...";

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
            this.Show();
            LoadNews();

            if (installDirs.Count == 0)
            {
                ShowFirstRun();
            }

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(5)
            };
            timer.Tick += OnTimerTick;
            timer.Start();

            progressText = "Ready to check for updates.";
        }

        void OnTimerTick(object sender, EventArgs e)
        {
            progressBar.Value = progress;
            progressBarText.Content = progressText;
        }

        private void ShowFirstRun()
        {
            System.Windows.Forms.MessageBox.Show("Welcome to the NI Updater.\n\nPlease set the Installation Path to continue.", "Welcome", MessageBoxButtons.OK, MessageBoxIcon.Information);
            progressBarText.Content = "Please set an installation path.";
            MenuOpenPathEditor(null, null);
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
                installDirs = new List<InstallPath>();
            }
            foreach (InstallPath install in installDirs)
            {
                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem
                {
                    Header = install.Name,
                };
                mi.Click += new RoutedEventHandler(MenuInstallDir_Click);
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
            miAdd.Click += new RoutedEventHandler(MenuOpenPathEditor);
            miAdd.IsCheckable = false;
            menuInstallDirs.Items.Add(miAdd);

            if (installDirs.Count > 0)
            {
                btn_update.IsEnabled = true;
                progressText = "Ready to check for updates.";
            }
            else
            {
                btn_update.IsEnabled = false;
                progressText = "Please set an installation path.";
            }
        }

        private void MenuInstallDir_Click(Object sender, RoutedEventArgs e)
        {
            filesVerified = false;
            updateRequired = false;
            btn_update.Content = "Verify";
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

        private void MenuOpenPathEditor(Object sender, RoutedEventArgs e)
        {
            InstallPathWindow ipw = new InstallPathWindow(installDirs);
            ipw.Owner = this;
            if ((bool)ipw.ShowDialog())
            {
                Properties.Settings.Default.installDirs = JsonConvert.SerializeObject(installDirs);
                Properties.Settings.Default.Save();
            }
            LoadInstallDirs();
        }


        #region News
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

        private void list_news_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (list_news.SelectedIndex > -1 && list_news.SelectedIndex < news.Count)
            {
                SetNews(news[list_news.SelectedIndex].message);
            }
        }
        #endregion


        #region Update
        private async void Button_Update_Click(object sender, RoutedEventArgs e)
        {
            if (updateRequired)
            {
                //DO UPDATE
                await UpdateGameFilesAsync();
            }
            else if (filesVerified)
            {
                //PLAY GAME
            }
            else
            {
                //VERIFY FILES
                await VerifyGameFiles();
            }
        }

        private async Task<Boolean> VerifyGameFiles()
        {
            
            filesVerified = false;
            updateRequired = false;
            progress = 0;
            progressText = "Starting verification...";
            this.Dispatcher.Invoke(() =>
            {
                btn_update.IsEnabled = false;
                btn_update.Content = "Verifying...";
                //progressBar.Value = 0;
                //progressBarText.Content = ;
            });
            var failed = false;

            try
            {
                if (ActiveInstall.Path == "")
                {
                    throw new Exception("Install directory not set.");
                }
                else if (!Directory.Exists(ActiveInstall.Path))
                {
                    updateRequired = true;
                    progressText = "Update is required";
                    this.Dispatcher.Invoke(() =>
                    {
                        //progressBarText.Content = "Update is required";
                        btn_update.Content = "Update";
                        btn_update.IsEnabled = true;
                    });
                    return true;
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


                updateRequired = false;
                hashObject.Source = ActiveInstall.Path;
                if (!Directory.Exists(hashObject.Source))
                {
                    updateRequired = true;
                }

                Queue pending = new Queue(hashObject.getFiles());
                List<Task<UpdateHashItem>> working = new List<Task<UpdateHashItem>>();
                float count = pending.Count;

                while (pending.Count + working.Count != 0)
                {
                    if (working.Count < 4 && pending.Count != 0)
                    {
                        var item = (UpdateHashItem)pending.Dequeue();
                        working.Add(Task.Run(() => VerifyItem(item)));
                    }
                    else
                    {
                        Task<UpdateHashItem> t = await Task.WhenAny(working);
                        working.RemoveAll(x => x.IsCompleted);
                        if (!t.Result.Verified) updateRequired = true;
                        progress = ((count - pending.Count) / count) * 100;
                        progressText = "Checking files... " + (count - pending.Count) + " / " + count;

                        //this.Dispatcher.Invoke(() =>
                        //{
                        //    progressBarText.Content = "Checking files... " + (count - pending.Count) + " / " + count;
                        //    progressBar.Value = ((count - pending.Count) / count) * 100;
                        //});
                    }
                }
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
                        progressText = "Incorrect beta password";
                        progress = 0;
                        this.Dispatcher.Invoke(() =>
                        {
                            System.Windows.Forms.MessageBox.Show("Unabled to authenticate with download server.\n\nBeta password is incorrect.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        });
                    }
                    else
                    {
                        if (ex.InnerException != null)
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                System.Windows.Forms.MessageBox.Show("Unabled to verify files. \n\n" + ex.InnerException.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            });
                        }
                        else
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                System.Windows.Forms.MessageBox.Show("Unabled to verify files. \n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            });
                        }
                        progressText = "Error verifying files";
                        progress = 0;
                    }
                }
            }
            if (updateRequired)
            {
                progressText = "Update is required - Latest version: " + hashObject.ModuleVersion;
                progress = 0;
                this.Dispatcher.Invoke(() =>
                {
                    btn_update.Content = "Update";
                    btn_update.IsEnabled = true;
                });
            }
            else if (!filesVerified)
            {
                progressText = "Error - Unable to verify files.";
                progress = 0;
                this.Dispatcher.Invoke(() =>
                {
                    btn_update.Content = "Verify";
                    btn_update.IsEnabled = true;
                });
            }
            else
            {
                progressText = "Current version: " + hashObject.ModuleVersion + " - Ready to play";
                progress = 100;
                this.Dispatcher.Invoke(() =>
                {
                    btn_update.Content = "Play";
                    btn_update.IsEnabled = true;
                });
                filesVerified = true;
            }
            return true;
        }

        private UpdateHashItem VerifyItem(UpdateHashItem item)
        {
            if (File.Exists(hashObject.Source + item.Path))
            {
                using (FileStream stream = new FileStream(ActiveInstall.Path + item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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

        private async Task UpdateGameFilesAsync()
        {
            this.Dispatcher.Invoke(() =>
            {
                btn_update.IsEnabled = false;
                btn_update.Content = "Updating...";
                progressBar.Value = 0;
                progressBarText.Content = "Starting verification...";
            });
            if (!hasWriteAccessToFolder(ActiveInstall.Path))
            {
                //Generate the folder & set permissions to allow us to update the files
                Process updater = new Process();
                if (System.Environment.OSVersion.Version.Major >= 6)
                {
                    updater.StartInfo.Verb = "runas"; //Run as admin, for UAC prompts
                }
                updater.StartInfo.Verb = "runas"; //Run as admin, for UAC prompts
                updater.StartInfo.FileName = "updater-permissions.exe";
                updater.StartInfo.Arguments = "\"" + ActiveInstall.Path.Replace("\\", "\\\\") + "\"";
                updater.StartInfo.UseShellExecute = true;
                updater.Start();
                updater.WaitForExit();
                updater.Close();
            }

            if (Directory.Exists(ActiveInstall.Path))
            {
                if (hasWriteAccessToFolder(ActiveInstall.Path))
                {
                    await Task.Run(async () => await Update_Game_Files());
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Unable to create the NordInvasion directory", "Error - Unable to continue.");
            }
            this.Dispatcher.Invoke(() =>
            {
                btn_update.IsEnabled = true;
                btn_update.Content = "Here!";
                progressBar.Value = 0;
                progressBarText.Content = "Update done";
            });
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
                    if (item.Verified == false)
                    {
                        progress = ((count - pending.Count) / count) * 100;
                        progressText = "Updating files... " + (count - pending.Count) + " / " + count;
                        working.Add(Task.Run(async () => await Update_Item(item)));
                    }
                }
                else
                {
                    Task<UpdateHashItem> t = await Task.WhenAny(working);
                    working.RemoveAll(x => x.IsCompleted);
                    if (t.Result.Verified)
                    {
                        progress = ((count - pending.Count) / count) * 100;
                        progressText = "Updating files... " + (count - pending.Count) + " / " + count;
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

        private bool hasWriteAccessToFolder(string folderPath)
        {
            try
            {
                // Attempt to get a list of security permissions from the folder. 
                // This will raise an exception if the path is read only or do not have access to view the permissions. 
                System.Security.AccessControl.DirectorySecurity ds = Directory.GetAccessControl(folderPath);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
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

        #endregion


        private void windowKeyPress(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) // Is Alt key pressed
            {
                if (Keyboard.IsKeyDown(Key.D) && Keyboard.IsKeyDown(Key.E) && Keyboard.IsKeyDown(Key.V))
                {
                    DevMenu.Visibility = Visibility.Visible;
                }
            }
        }




        #region Dev Controls

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

        private void Dev_Clear_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.installDirs = "{ }";
            Properties.Settings.Default.Save();
            LoadInstallDirs();
        }
        #endregion
    }
}

