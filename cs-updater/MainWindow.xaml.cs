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
        private HttpClient client = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(300)
        };
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

            SetNews(this.FindResource("NewsLoading") as string);
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
            try
            {
                String versionString = await Task.Run(() => Download_JSON_File(Properties.Settings.Default.UpdaterVersionCheck));

                if (versionString != null && (versionString.StartsWith("[") || versionString.StartsWith("{")))
                {
                    UpdaterVersion UpdateJson = await Task.Run(() => JsonConvert.DeserializeObject<UpdaterVersion>(versionString));
                    if (Version.Parse(UpdateJson.version) > Version.Parse(Properties.Settings.Default.Version))
                    {
                        NotificationWindow nw = new NotificationWindow("Update_Title",
                            new List<NotificationWindowItem> {
                                new NotificationWindowItem("Update_Text1"),
                                new NotificationWindowItem("", false),
                                new NotificationWindowItem("Update_Text2") },
                            3)
                        {
                            Owner = this
                        };
                        nw.ShowDialog();

                        if (nw.Result > 0)
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
            NotificationWindow nw = new NotificationWindow("Welcome_Title",
                new List<NotificationWindowItem> {
                    new NotificationWindowItem("Welcome1"),
                    new NotificationWindowItem("", false),
                    new NotificationWindowItem("Welcome2") },
                0)
            {
                Owner = this
            };
            nw.ShowDialog();

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
                SetNews(this.FindResource("NewsFailed") as string);
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
        /// <summary>
        /// Just calls DoUpdate to actually do the stuff
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Update_Click(object sender, RoutedEventArgs e)
        {
            DoUpdate();
        }

        /// <summary>
        /// Actual functionality behind the button but can run async
        /// </summary>
        private async void DoUpdate()
        {
            try
            {
                menuInstallDirs.IsEnabled = false;
                menuOptions.IsEnabled = false;
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
                        menuInstallDirs.IsEnabled = true;
                        menuOptions.IsEnabled = true;
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
                        menuInstallDirs.IsEnabled = true;
                        menuOptions.IsEnabled = true;
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
                        menuInstallDirs.IsEnabled = true;
                        menuOptions.IsEnabled = true;
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
                    menuInstallDirs.IsEnabled = true; menuOptions.IsEnabled = true;
                    var errMessage = "";
                    if (filesVerified && !updateRequired)
                    {
                        errMessage = "Error_Launching";
                    }
                    else if (updateRequired)
                    {
                        errMessage = "Error_Update";
                    }
                    else if (!filesVerified)
                    {
                        errMessage = "Error_Verify";
                    }
                    filesVerified = false;
                    updateRequired = false;

                    if (ex.InnerException.Message.StartsWith("Error_"))
                    {
                        NotificationWindow nw = new NotificationWindow("Error",
                            new List<NotificationWindowItem> {
                                new NotificationWindowItem(errMessage),
                                new NotificationWindowItem("", false),
                                new NotificationWindowItem(ex.InnerException.Message),
                                new NotificationWindowItem("", false),
                                new NotificationWindowItem("Error_Contact") },
                            0)
                        {
                            Owner = this
                        };
                        nw.ShowDialog();
                    }
                    else if (ex.Message.StartsWith("Error_"))
                    {
                        NotificationWindow nw = new NotificationWindow("Error",
                            new List<NotificationWindowItem> {
                                new NotificationWindowItem(errMessage),
                                new NotificationWindowItem("", false),
                                new NotificationWindowItem(ex.Message),
                                new NotificationWindowItem("", false),
                                new NotificationWindowItem("Error_Contact") },
                            0)
                        {
                            Owner = this
                        };
                        nw.ShowDialog();
                    }
                    else
                    {
                        NotificationWindow nw = new NotificationWindow("Error",
                            new List<NotificationWindowItem> {
                                new NotificationWindowItem(errMessage),
                                new NotificationWindowItem("", false),
                                new NotificationWindowItem("Error_Contact") },
                            0)
                        {
                            Owner = this
                        };
                        nw.ShowDialog();
                    }
                });
            }

        }

        /// <summary>
        /// Master driver for verifying items. Spawns process to check actual item.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
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
                    throw new Exception("Error_No_Dir");
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
                    throw new Exception("Error_Server_Connection");
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
                        if (t.Result.Verified != true)
                        {
                            updateRequired = true;
                        }
                        working.Remove(t);
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
                            NotificationWindow nw = new NotificationWindow("Error",
                                new List<NotificationWindowItem> {
                                    new NotificationWindowItem("Error_Beta_Auth"),
                                    new NotificationWindowItem("", false),
                                    new NotificationWindowItem("Error_Beta_Pw"),
                                    new NotificationWindowItem("", false),
                                    new NotificationWindowItem("Error_Contact") },
                                0)
                            {
                                Owner = this
                            };
                            nw.ShowDialog();
                        });
                    }
                    else
                    {
                        if (ex.InnerException != null)
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                NotificationWindow nw = new NotificationWindow("Error",
                                    new List<NotificationWindowItem> {
                                        new NotificationWindowItem("Error_Verify"),
                                        new NotificationWindowItem("", false),
                                        new NotificationWindowItem(ex.InnerException.Message, false),
                                        new NotificationWindowItem("", false),
                                        new NotificationWindowItem("Error_Contact") },
                                    0)
                                {
                                    Owner = this
                                };
                                nw.ShowDialog();
                            });
                        }
                        else
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                NotificationWindow nw = new NotificationWindow("Error",
                                        new List<NotificationWindowItem> {
                                        new NotificationWindowItem("Error_Verify"),
                                        new NotificationWindowItem("", false),
                                        new NotificationWindowItem(ex.Message, false),
                                        new NotificationWindowItem("", false),
                                        new NotificationWindowItem("Error_Contact") },
                                        0)
                                {
                                    Owner = this
                                };
                                nw.ShowDialog();
                            });
                        }
                        SetProgressBarText("PB_verifyFail");
                        progress = 0;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Checks inividual item checksums.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
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
                            //logger.Info(item.Name + " - local: " + BitConverter.ToString(checksum).Replace("-", string.Empty).ToLower() + " - json: " + item.Crc);
                            item.Verified = true;
                        }
                        else
                        {
                            //logger.Info(item.Name + " - local: " + BitConverter.ToString(checksum).Replace("-", string.Empty).ToLower() + " - json: " + item.Crc);
                            item.Verified = false;
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

        /// <summary>
        /// Holder of the update process. Does some folder stuff.
        /// </summary>
        /// <returns></returns>
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
                        logger.Info("UAC attempts triggered: " + uacCount.ToString());
                        if (uacCount > 2) throw new Exception("Error_Permissions");
                    }
                }
                else
                {
                    logger.Error("Can't create directory");
                    throw new Exception("Error_Creating");
                }
            }
            else
            {
                logger.Error("Can't create directory");
                throw new Exception("Error_Creating");
            }
        }

        /// <summary>
        /// Holds the queue of all items needing updated and watches progress.
        /// </summary>
        /// <returns></returns>
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
                        throw new Exception("Error_Creating_Permissions");
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
                if (working.Count < Properties.Settings.Default.Threads_Download && pending.Count > 0)
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
                            else
                            {
                                working.Remove(t);
                                pending.Enqueue(t.Result);
                                errors++;
                            }
                        }
                    }
                }
            }

            if (errors >= 50)
            {
                logger.Error("Download Errors - Writable false and attempted true.");
                throw new Exception("Error_Writing_Permissions");
            }
            else if (errors >= 4)
            {
                logger.Error("Download Errors - Tried to download too many files (4) and failed.");
                throw new Exception("Error_Download_Excess");
            }
            updateRequired = false;
            filesVerified = true;
            return true;
        }

        /// <summary>
        /// Updates the relevant item
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
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
            if (item.Attempts > 3) throw new Exception("Error_Server_Files");
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
                throw new Exception("Error_UAC_Denied");
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw new Exception("Error_UAC_Unknown");
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
                    NotificationWindow nw = new NotificationWindow("Error",
                        new List<NotificationWindowItem> {
                            new NotificationWindowItem("No_Launcher1"),
                            new NotificationWindowItem("", false),
                            new NotificationWindowItem("No_Launcher2")
                        }, 0)
                    {
                        Owner = this
                    };
                    nw.ShowDialog();
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

        private void Menu_Logs_Click(object sender, RoutedEventArgs e)
        {
            string cmd = "explorer.exe";
            string arg = "/e, " + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NordInvasion", "Launcher");
            Process.Start(cmd, arg);
        }

        private void Menu_About_Click(object sender, RoutedEventArgs e)
        {

            NotificationWindow nw = new NotificationWindow("About",
                new List<NotificationWindowItem> {
                    new NotificationWindowItem("About1"),
                    new NotificationWindowItem(Properties.Settings.Default.Version + "\n", false),
                    new NotificationWindowItem("", false),
                    new NotificationWindowItem("About2"),
                    new NotificationWindowItem("About3"), },
                1)
            {
                Owner = this
            };
            nw.ShowDialog();
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
            Properties.Settings.Default.Language = mi.Tag.ToString();
            Properties.Settings.Default.Save();
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

