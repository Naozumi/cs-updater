using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections;
using System.Diagnostics;
using NLog;
using System.Windows.Input;
using cs_updater_lib;
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
        private List<HostServer> servers = new List<HostServer>();
        private List<InstallPath> installDirs = new List<InstallPath>();
        private InstallPath ActiveInstall = new InstallPath();
        private List<News> news = new List<News>();
        private Boolean allowWebNavigation = true;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private bool updateRequired = false;
        private bool filesVerified = false;
        private Object locker = new Object();
        private bool WritableAttempted = true;

        private double progress = 0;
        private string progressValue = "";

        public MainWindow()
        {
            InitializeComponent();
            logger.Info("Current Version: " + Properties.Settings.Default.Version);
            System.Net.ServicePointManager.DefaultConnectionLimit = 20;

            LocUtil.SetDefaultLanguage(this);
            SetProgressBarText("PB_loading");

            foreach (System.Windows.Controls.MenuItem item in menuItemLanguages.Items)
            {
                if (item.Tag.ToString().Equals(LocUtil.GetCurrentCultureName(this)))
                    item.IsChecked = true;
            }

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
            CheckForUpdate();
        }

        void OnTimerTick(object sender, EventArgs e)
        {
            progressBar.Value = progress;
            progressBarTextVersion.Content = progressValue;
            taskBarItemInfo.ProgressValue = progress / 100;
        }

        private void SetProgressBarText(string text)
        {
            progressBarText.SetResourceReference(System.Windows.Controls.Label.ContentProperty, text);
            progressValue = "";
            progressBarTextVersion.Content = progressValue;
        }

        private void SetButtonText(string text)
        {
            btn_update_text.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, text);
        }

        private async void CheckForUpdate()
        {
            SetNews("Checking for update...");
            try
            {
                String versionString = await Task.Run(() => Download_JSON_File(Properties.Settings.Default.UpdaterVersionCheck));

                if (versionString != null && (versionString.StartsWith("[") || versionString.StartsWith("{")))
                {
                    UpdaterVersion UpdateJson = await Task.Run(() => JsonConvert.DeserializeObject<UpdaterVersion>(versionString));
                    if (Version.Parse(UpdateJson.version) > Version.Parse(Properties.Settings.Default.Version))
                    {
                        var answer = System.Windows.Forms.MessageBox.Show("Update available for the updater.\n\nClick \"OK\" to download.",
                        "Update required", System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Information);
                        if (answer == System.Windows.Forms.DialogResult.OK)
                        {
                            System.Diagnostics.Process.Start(UpdateJson.url);
                            System.Windows.Application.Current.Shutdown();
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error with updater version check.");
                logger.Error(ex);
            }


            //else carry on with normal operation
            LoadInstallDirs();
            this.Show();
            LoadNews();

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(5)
            };
            timer.Tick += OnTimerTick;
            timer.Start();

            if (installDirs.Count == 0)
            {
                ShowFirstRun();
            }
            else if (Properties.Settings.Default.AutoVerify)
            {
                DoUpdate(); //trigger update of files
            }
        }

        private void ShowFirstRun()
        {
            System.Windows.Forms.MessageBox.Show("Welcome to the NI Launcher.\n\nPlease set the Installation Path to continue.", "Welcome", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            SetProgressBarText("PB_missingInstall");
            activeInstallText.Content = " -";
            Menu_OptionsClick(null, null);
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
                    activeInstallText.Content = " " + ActiveInstall.Name;
                }
                menuInstallDirs.Items.Add(mi);
            }

            if (installDirs.Count > 0)
            {
                this.Dispatcher.Invoke(() =>
                {
                    SetButtonText("B_verify");
                    btn_update.IsEnabled = true;
                });
                SetProgressBarText("PB_verify");
                filesVerified = false;
            }
            else
            {
                btn_update.IsEnabled = false;
                SetProgressBarText("PB_missingInstall");
                activeInstallText.Content = " -";
            }
        }

        private void MenuInstallDir_Click(Object sender, RoutedEventArgs e)
        {
            filesVerified = false;
            updateRequired = false;
            SetButtonText("B_verify");
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
            //menuInstallDirs.Header = "Active Installation: " + ActiveInstall.Name;
            activeInstallText.Content = " " + ActiveInstall.Name;
            SetProgressBarText("PB_verify");
        }

        private void Menu_OptionsClick(Object sender, RoutedEventArgs e)
        {
            OptionsWindow ipw = new OptionsWindow(installDirs);
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
            try
            {
                String newsString = await Task.Run(() => Download_JSON_File(Properties.Settings.Default.newsUrl));

                if (newsString != null && (newsString.StartsWith("[") || newsString.StartsWith("{")))
                {
                    news = await Task.Run(() => JsonConvert.DeserializeObject<List<News>>(newsString));
                    foreach (var item in news)
                    {
                        list_news.Items.Add(item.subject);
                    }
                    list_news.SelectedItem = list_news.Items.GetItemAt(0);
                }
            }
            catch (Exception ex)
            {
                SetNews("Unable to load news.");
                logger.Error("Unable to load news.");
                logger.Error(ex);
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

        private void Open_Thread_Click(object sender, RoutedEventArgs e)
        {
            if (list_news.SelectedIndex > -1 && list_news.SelectedIndex < news.Count)
            {
                System.Diagnostics.Process.Start("http://forum.nordinvasion.com/showthread.php?tid=" + news[list_news.SelectedIndex].tid);
            }
        }
        #endregion


        #region Verify_&_Update
        private void Button_Update_Click(object sender, RoutedEventArgs e)
        {
            DoUpdate();
        }

        private async void DoUpdate()
        {
            try
            {
                menuSettings.IsEnabled = false;
                taskBarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
                WritableAttempted = false;

                if (updateRequired)
                {
                    //DO UPDATE
                    await UpdateGameFilesAsync();
                }
                else if (filesVerified)
                {
                    //PLAY GAME
                    RunGame();
                }
                else
                {
                    //VERIFY FILES
                    await VerifyGameFiles();
                }

                if (updateRequired)
                {
                    SetProgressBarText("PB_updateRequired");
                    progressValue = " " + hashObject.ModuleVersion;
                    progress = 0;
                    this.Dispatcher.Invoke(() =>
                    {
                        SetButtonText("B_update");
                        btn_update.IsEnabled = true;
                        menuSettings.IsEnabled = true;
                    });
                    if (Properties.Settings.Default.AutoUpdate)
                    {
                        DoUpdate(); //trigger update of files
                    }
                }
                else if (!filesVerified)
                {
                    SetProgressBarText("PB_verifyFail");
                    progress = 0;
                    this.Dispatcher.Invoke(() =>
                    {
                        SetButtonText("B_verify");
                        btn_update.IsEnabled = true;
                        menuSettings.IsEnabled = true;
                    });
                }
                else
                {
                    SetProgressBarText("PB_readyToPlay");
                    progressValue = " " + hashObject.ModuleVersion;
                    progress = 100;

                    this.Dispatcher.Invoke(() =>
                    {
                        SetButtonText("B_play");
                        btn_update.IsEnabled = true;
                        menuSettings.IsEnabled = true;
                    });

                    filesVerified = true;
                }
                taskBarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
            }
            catch (Exception ex)
            {
                logger.Error("Error - \"Do Update\"");
                logger.Error(ex);
                SetProgressBarText("PB_updateError");
                progress = 0;
                taskBarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
                this.Dispatcher.Invoke(() =>
                {
                    SetButtonText("B_verify");
                    btn_update.IsEnabled = true;
                    menuSettings.IsEnabled = true;
                    var errMessage = "";
                    if (filesVerified && !updateRequired)
                    {
                        errMessage = "Error launching game.\n\nIf the error persists then please contact the developers via forum.nordinvasion.com";
                    }
                    else if (updateRequired)
                    {
                        errMessage = ex.Message;
                    }
                    else if (!filesVerified)
                    {
                        errMessage = "Error verifying files.\n\nIf the error persists then please contact the developers via forum.nordinvasion.com";
                    }
                    filesVerified = false;
                    updateRequired = false;

                    System.Windows.Forms.MessageBox.Show(errMessage, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                });
            }

        }

        private async Task<Boolean> VerifyGameFiles()
        {

            filesVerified = false;
            updateRequired = false;
            progress = 0;
            SetProgressBarText("PB_verifyStart");
            this.Dispatcher.Invoke(() =>
            {
                btn_update.IsEnabled = false;
                SetButtonText("B_verifyProgress");
            });
            var failed = false;

            try
            {
                if (ActiveInstall.Path == "")
                {
                    throw new Exception("Install directory not set.");
                }

                SetProgressBarText("PB_downloadHash");

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
                if (!master.Url.EndsWith("/")) master.Url += "/";
                servers.Add(master);
                foreach (HostServer host in hosts)
                {
                    if (host.Working && host != master && (Version.Parse(host.Json.ModuleVersion) == Version.Parse(master.Json.ModuleVersion)))
                    {
                        if (!host.Url.EndsWith("/")) host.Url += "/";
                        servers.Add(host);
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

                hashObject = master.Json;

                updateRequired = false;
                hashObject.Source = ActiveInstall.Path;
                if (!Directory.Exists(hashObject.Source))
                {
                    updateRequired = true;
                    return true;
                }

                //Verify Files
                Queue pending = new Queue(hashObject.getFiles());
                List<Task<UpdateHashItem>> working = new List<Task<UpdateHashItem>>();
                float count = pending.Count;
                SetProgressBarText("PB_verifyProgress");

                while (pending.Count + working.Count != 0)
                {
                    if (working.Count < Properties.Settings.Default.Threads_Check && pending.Count != 0)
                    {
                        var item = (UpdateHashItem)pending.Dequeue();
                        working.Add(Task.Run(() => VerifyItem(item)));
                    }
                    else
                    {
                        Task<UpdateHashItem> t = await Task.WhenAny(working);
                        working.RemoveAll(x => x.IsCompleted);
                        progress = ((count - pending.Count) / count) * 100;
                        progressValue = " " + (count - pending.Count) + " / " + count;
                    }
                }
                if (updateRequired == false) filesVerified = true;
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
                        SetProgressBarText("PB_betaPasswordError");
                        progress = 0;
                        this.Dispatcher.Invoke(() =>
                        {
                            System.Windows.Forms.MessageBox.Show("Unabled to authenticate with download server.\n\nBeta password is incorrect.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                        });
                    }
                    else
                    {
                        if (ex.InnerException != null)
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                System.Windows.Forms.MessageBox.Show("Unabled to verify files. \n\n" + ex.InnerException.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                            });
                        }
                        else
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                System.Windows.Forms.MessageBox.Show("Unabled to verify files. \n\n" + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                            });
                        }
                        SetProgressBarText("PB_verifyFail");
                        progress = 0;
                    }
                }
            }
            return true;
        }

        private UpdateHashItem VerifyItem(UpdateHashItem item)
        {
            try
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
                else
                {
                    item.Verified = false;
                    updateRequired = true;
                }
                return item;
            }
            catch
            {
                item.Verified = false;
                updateRequired = true;
                return item;
            }
        }

        private async Task UpdateGameFilesAsync()
        {
            this.Dispatcher.Invoke(() =>
            {
                btn_update.IsEnabled = false;
                SetButtonText("B_updateProgress");
                progressBar.Value = 0;
                SetProgressBarText("PB_updateStart");
            });
            if (!hasWriteAccessToFolder(ActiveInstall.Path))
            {
                //Generate the folder & set permissions to allow us to update the files
                MakeFilesWriteable();
                WritableAttempted = false;
            }

            if (Directory.Exists(ActiveInstall.Path))
            {
                if (hasWriteAccessToFolder(ActiveInstall.Path))
                {
                    int uacCount = 0;
                    while (await Update_Game_Files() == false)
                    {
                        uacCount++;
                        if (uacCount > 2) throw new Exception("Unable to write the files - insufficient permissions.");
                    }
                }
                else
                {
                    throw new Exception("Unable to create the NordInvasion directory.");
                }
            }
            else
            {
                throw new Exception("Unable to create the NordInvasion directory.");
            }
        }

        private async Task<Boolean> Update_Game_Files()
        {
            hashObject.Source = ActiveInstall.Path;
            SetProgressBarText("PB_updateProgress");

            foreach (UpdateHashItem f in hashObject.getFolders())
            {
                try
                {
                    System.IO.Directory.CreateDirectory(ActiveInstall.Path + f.Path);
                }
                catch
                {
                    logger.Info("Can't create directory: " + f.Path + "  - Running Permission Setter");
                    MakeFilesWriteable();
                    try
                    {
                        System.IO.Directory.CreateDirectory(ActiveInstall.Path + f.Path);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(new Exception("Cannot create folder: " + f.Path, ex));
                        throw new Exception("Unable to create the neccessary folders - insufficient permissions.");
                    }
                }
            }

            Queue pending = new Queue();
            List<Task<UpdateHashItem>> working = new List<Task<UpdateHashItem>>();
            var errors = 0;
            
            foreach (UpdateHashItem f in hashObject.getFiles())
            {
                if (f.Verified == false)
                {
                    pending.Enqueue(f);
                }
            }
            float count = pending.Count;

            while (pending.Count + working.Count != 0 && errors < 30)
            {
                if (working.Count < Properties.Settings.Default.Threads_Download && pending.Count != 0)
                {
                    var item = (UpdateHashItem)pending.Dequeue();
                    if (item.Verified == false)
                    {
                        progress = ((count - pending.Count) / count) * 100;
                        progressValue = " " + (count - pending.Count) + " / " + count;
                        working.Add(Task.Run(async () => await Update_Item(item)));
                    }
                }
                else
                {
                    Task<UpdateHashItem> t = await Task.WhenAny(working);
                    if (t.Result.Verified)
                    {
                        working.Remove(t);
                        progress = ((count - pending.Count) / count) * 100;
                        progressValue = " " + (count - pending.Count) + " / " + count;
                    }
                    else
                    {
                        lock (locker)
                        {
                            if (t.Result.Writable == false && !WritableAttempted)
                            {
                                pending.Clear();
                                MakeFilesWriteable();
                                return false;                              
                            }
                            else if (t.Result.Writable == false && WritableAttempted)
                            {
                                pending.Clear();
                                errors += 60;
                                working.Remove(t);
                            }
                        }
                    }
                }
            }

            if (errors >= 60)
            {
                throw new Exception("Unable to write the files - insufficient permissions.");
            }
            else if (errors >= 30)
            {
                throw new Exception("Unable to download files from server. Please contact NI Support.");
            }
            updateRequired = false;
            filesVerified = true;
            return true;
        }

        private async Task<UpdateHashItem> Update_Item(UpdateHashItem item)
        {

            HttpResponseMessage response;
            foreach (HostServer host in servers)
            {
                try
                {
                    response = await client.GetAsync(host.Url + "files/" + item.Crc + ".gz");
                    if (response.IsSuccessStatusCode)
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
                        }
                    }
                    else
                    {
                        // server said no
                        logger.Info("Failed to download: " + item.Name +
                        Environment.NewLine + " Attempt: " + item.Attempts.ToString() +
                        Environment.NewLine + "Reason: " + response.ReasonPhrase +
                        Environment.NewLine + "Request: " + response.RequestMessage);
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    item.Writable = false;
                    logger.Error(new Exception("Cannot write the file: " + item.Name + " WA: " + WritableAttempted + "  " + item.Path, ex));
                    return item;
                }
                catch (Exception ex)
                {
                    //web server sent an error message
                    logger.Info(new Exception("Error downloading the file: " + host.Url + "files/" + item.Crc + ".gz" + "  " + item.Path, ex));
                }
            }
            logger.Error(new Exception("Cannot download file - tried all (" + servers.Count.ToString() + ") hosts. Filename: " + item.Crc + ".gz" + " -- " + item.Path));
            item.Attempts++;
            if (item.Attempts > 3) throw new Exception("Unable to find a server to download file(s).");
            return item;
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

        private bool MakeFilesWriteable()
        {
            if (WritableAttempted) return false;
            WritableAttempted = true;
            Process updater = new Process();
            if (System.Environment.OSVersion.Version.Major >= 6)
            {
                updater.StartInfo.Verb = "runas"; //Run as admin, for UAC prompts
            }
            updater.StartInfo.Verb = "runas"; //Run as admin, for UAC prompts
            updater.StartInfo.FileName = "updater-permissions.exe";
            updater.StartInfo.Arguments = "\"" + ActiveInstall.Path.Replace("\\", "\\\\") + "\"";
            updater.StartInfo.UseShellExecute = true;
            try
            {
                updater.Start();
                updater.WaitForExit();
                int existCode = updater.ExitCode;
                if (existCode < 1)
                {
                    updater.Close();
                    return false;
                }
                updater.Close();
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                logger.Error(ex);
                throw new Exception("Cannot update. Permission was denied when making the files writable.");
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw new Exception("Cannot update. Unknown error occured when trying to make the files writable.");
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
                    filename = Properties.Settings.Default.updateFile;
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


                HttpResponseMessage response = await client.PostAsync(url + Properties.Settings.Default.BetaCheck, formContent);
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

        private void RunGame()
        {
            if (ActiveInstall.Executable != "" && ActiveInstall.Executable != null)
            {
                Process warband = new Process();
                if (ActiveInstall.Executable.ToLower().EndsWith("steam.exe"))
                {
                    string[] words = ActiveInstall.Path.Split('\\');
                    warband.StartInfo.FileName = ActiveInstall.Executable;
                    warband.StartInfo.Arguments = "-applaunch 48700 -sm " + words[words.Length - 2];
                }
                else
                {
                    string[] words = ActiveInstall.Path.Split('\\');
                    warband.StartInfo.FileName = ActiveInstall.Executable;
                    warband.StartInfo.Arguments = "-sm " + words[words.Length - 2];
                }
                warband.StartInfo.UseShellExecute = false;
                warband.Start();
                this.Close();
            }
            else
            {
                this.Dispatcher.Invoke(() =>
                {
                    System.Windows.Forms.MessageBox.Show("No launcher configured - please set the path to steam or mb_warband.exe to enable launching the game.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                });
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

        private void Menu_Logs_Click(object sender, RoutedEventArgs e)
        {
            string cmd = "explorer.exe";
            string arg = "/e, " + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NordInvasion", "Updater");
            Process.Start(cmd, arg);
        }

        private void Menu_About_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.MessageBox.Show("NordInvasion Launcher\nVersion: " + Properties.Settings.Default.Version + "\n\nFor more info visit:\nhttps://nordinvasion.com",
                        "About", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
        }

        private void Menu_Lang_Click(object sender, RoutedEventArgs e)
        {
            foreach (System.Windows.Controls.MenuItem item in menuItemLanguages.Items)
            {
                item.IsChecked = false;
            }

            System.Windows.Controls.MenuItem mi = sender as System.Windows.Controls.MenuItem;
            mi.IsChecked = true;
            LocUtil.SwitchLanguage(this, mi.Tag.ToString());
        }

        #region Dev Controls
        private void Dev_Clear_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.installDirs = "{ }";
            Properties.Settings.Default.Save();
            LoadInstallDirs();
        }
        private void Writable_Click(object sender, RoutedEventArgs e)
        {
            WritableAttempted = false;
            MakeFilesWriteable();
        }

        #endregion
    }
}

