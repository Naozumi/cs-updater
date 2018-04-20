using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using cs_updater_lib;
using NLog;
using System.Text.RegularExpressions;

namespace cs_updater
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        private OptionsHelp help = null;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public OptionsWindow(List<InstallPath> installs)
        {
            this.Installs = installs;
            InitializeComponent();
            LocUtil.SetDefaultLanguage(this);
            data.ItemsSource = this.Installs;
            data.Items.Refresh();
            cb_verify.IsChecked = Properties.Settings.Default.AutoVerify;
            cb_update.IsChecked = Properties.Settings.Default.AutoUpdate;
            tb_threads_ch.Text = Properties.Settings.Default.Threads_Check.ToString();
            tb_threads_dl.Text = Properties.Settings.Default.Threads_Download.ToString();

            if (this.Installs.Count == 0) Help_Click(null, null);
        }

        public List<InstallPath> Installs { get; set; }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button btn = sender as System.Windows.Controls.Button;
            string initialPath = null;
            if (btn.Tag == null || (string)btn.Tag == "")
            {
                initialPath = @"C:\";
            }
            else
            {
                initialPath = (string)btn.Tag;
            }
            DirectoryInfo dir = null;

            CommonOpenFileDialog dialog = new CommonOpenFileDialog
            {
                InitialDirectory = initialPath,
                IsFolderPicker = true,
                Title = this.FindResource("Warband_Modules") as string
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                dir = new DirectoryInfo(dialog.FileName);
            }

            if (dir == null)
            {
                this.Activate();
                return;
            }

            if (!(dir.FullName.ToUpper().Contains(@"\MODULES") || dir.FullName.ToUpper().EndsWith(@"\MODULES\NORDINVASION")))
            {
                NotificationWindow nw = new NotificationWindow("Download_Confim_Title",
                    new List<NotificationWindowItem> {
                        new NotificationWindowItem("Download_Confim1"),
                        new NotificationWindowItem("", false),
                        new NotificationWindowItem("Download_Confim2") },
                    3)
                {
                    Owner = this
                };
                nw.ShowDialog();

                if (nw.Result < 1)
                {
                    this.Activate();
                    return;
                }
            }

            string path = "";
            if (dir.FullName.ToUpper().EndsWith(@"\MODULES"))
            {
                path = dir.FullName + @"\NordInvasion";
            }
            else
            {
                path = dir.FullName;
            }

            InstallPath selected = (InstallPath)data.CurrentCell.Item;
            selected.Path = path;

            //Sends the enter event - on a datagrid, this auto adds a new blank row. Generally, it just looks a bit better.
            this.data.CommitEdit();
            var key = Key.Enter;
            var target = Keyboard.FocusedElement;
            var routedEvent = Keyboard.KeyDownEvent;

            target.RaiseEvent(
              new System.Windows.Input.KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(this),
                0,
                key)
              { RoutedEvent = routedEvent }
            );

            this.data.CommitEdit();
            key = Key.Up;
            target = Keyboard.FocusedElement;
            routedEvent = Keyboard.KeyDownEvent;
            target.RaiseEvent(
              new System.Windows.Input.KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(this),
                0,
                key)
              { RoutedEvent = routedEvent }
            );
            this.Activate();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            //If theres only one, then set it as default
            if (Installs.Count == 1)
            {
                Installs[0].IsDefault = true;
            }

            int d = 0;
            foreach (InstallPath install in Installs)
            {
                if (install.IsDefault) d++;
                if (install.Path == "" || install.Path == null)
                {
                    NotificationWindow nw = new NotificationWindow("Dir_No_Path_Title",
                    new List<NotificationWindowItem> {
                        new NotificationWindowItem("Dir_No_Path1"),
                        new NotificationWindowItem("", false),
                        new NotificationWindowItem("Dir_No_Path2")
                    }, 0)
                    {
                        Owner = this
                    };
                    nw.ShowDialog();
                    return;
                }
                if (!install.Path.EndsWith(@"\")) install.Path += @"\";
            }


            if (Installs.Count == 0)
            {
                //No installs
                NotificationWindow nw = new NotificationWindow("Error",
                    new List<NotificationWindowItem> {
                        new NotificationWindowItem("Dir_Not_Set")
                    }, 0)
                {
                    Owner = this
                };
                nw.ShowDialog();
            }
            else if (d < 1)
            {
                //No installs
                NotificationWindow nw = new NotificationWindow("Error",
                    new List<NotificationWindowItem> {
                        new NotificationWindowItem("Default_Not_Set")
                    }, 0)
                {
                    Owner = this
                };
                nw.ShowDialog();
            }
            else if (d == 1)
            {
                // All OK
                DialogResult = true;
                Properties.Settings.Default.AutoVerify = (bool)cb_verify.IsChecked;
                Properties.Settings.Default.AutoUpdate = (bool)cb_update.IsChecked;
                int tc = Int32.Parse(tb_threads_ch.Text);
                if (tc > 20)
                {
                    tc = 20;
                }else if(tc < 1)
                {
                    tc = 4;
                }
                Properties.Settings.Default.Threads_Check = tc;
                tc = Int32.Parse(tb_threads_dl.Text);
                if (tc > 20)
                {
                    tc = 20;
                }
                else if (tc < 1)
                {
                    tc = 4;
                }
                Properties.Settings.Default.Threads_Download = tc;
                Properties.Settings.Default.Save();
                this.Close();
            }
            else if (d > 1)
            {
                //Too many defaults
                NotificationWindow nw = new NotificationWindow("Error",
                    new List<NotificationWindowItem> {
                        new NotificationWindowItem("Default_Excess1"),
                        new NotificationWindowItem("", false),
                        new NotificationWindowItem("Default_Excess2")
                    }, 0)
                {
                    Owner = this
                };
                nw.ShowDialog();
            }

        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }

        private void AutomaticallyAddInstalls(Object sender, RoutedEventArgs e)
        {
            try
            {
                List<InstallPath> foundInstalls = getInstallationDirectories();
                List<InstallPath> newInstalls = new List<InstallPath>();
                foreach (InstallPath foundInstall in foundInstalls)
                {
                    var item = Installs.FirstOrDefault(o => o.Path == foundInstall.Path);
                    if (item == null)
                    {
                        newInstalls.Add(foundInstall);
                    }
                }
                if (newInstalls.Count > 0)
                {
                    NotificationWindow nw = new NotificationWindow("Installs_Found_Title",
                    new List<NotificationWindowItem> {
                        new NotificationWindowItem("Installs_Found_Text"),
                        new NotificationWindowItem(newInstalls.Count.ToString(), false)
                    }, 0)
                    {
                        Owner = this
                    };
                    nw.ShowDialog();
                    Installs.AddRange(newInstalls);
                }
                else
                {
                    NotificationWindow nw = new NotificationWindow("Installs_Not_Found_Title",
                    new List<NotificationWindowItem> {
                        new NotificationWindowItem("Installs_Not_Found_Text")
                    }, 0)
                    {
                        Owner = this
                    };
                    nw.ShowDialog();
                }

                data.Items.Refresh();
            }
            catch (Exception ex)
            {
                logger.Error("Error finding directories");
                logger.Error(ex);
                NotificationWindow nw = new NotificationWindow("Error",
                    new List<NotificationWindowItem> {
                        new NotificationWindowItem("Installs_Fail")
                    }, 0)
                {
                    Owner = this
                };
                nw.ShowDialog();
            }
        }

        private List<InstallPath> getInstallationDirectories()
        {
            var installs = new List<InstallPath>();
            List<String> registry_key = new List<string>
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            if (Environment.Is64BitOperatingSystem)
            {
                foreach (string reg in registry_key)
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (Microsoft.Win32.RegistryKey key = hklm.OpenSubKey(reg))
                    {
                        if (key != null)
                        {
                            foreach (string subkey_name in key.GetSubKeyNames())
                            {
                                using (RegistryKey subkey = key.OpenSubKey(subkey_name))
                                {
                                    if (subkey.GetValue("DisplayName") != null)
                                    {
                                        if (subkey.GetValue("DisplayName").ToString().Contains("Mount") && subkey.GetValue("DisplayName").ToString().Contains("Warband"))
                                        {
                                            string lpath = "";
                                            if (subkey.GetValue("UninstallString").ToString().Contains("steam.exe"))
                                            {
                                                lpath = GetSteamPath();
                                                installs.Add(new InstallPath(subkey.GetValue("DisplayName").ToString(), subkey.GetValue("InstallLocation").ToString() + @"\Modules\NordInvasion\", "", false, lpath));
                                            }
                                            else if (File.Exists(subkey.GetValue("UninstallString").ToString().Replace("uninstall.exe", "mb_warband.exe"))) // if selected "Modules" folder
                                            {
                                                lpath = subkey.GetValue("UninstallString").ToString().Replace("uninstall.exe", "mb_warband.exe");
                                                installs.Add(new InstallPath(subkey.GetValue("DisplayName").ToString(), subkey.GetValue("UninstallString").ToString().Replace("uninstall.exe", "") + @"Modules\NordInvasion\", "", false, lpath));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (string reg in registry_key)
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    using (Microsoft.Win32.RegistryKey key = hklm.OpenSubKey(reg))
                    {
                        if (key != null)
                        {
                            foreach (string subkey_name in key.GetSubKeyNames())
                            {
                                using (RegistryKey subkey = key.OpenSubKey(subkey_name))
                                {
                                    if (subkey.GetValue("DisplayName") != null)
                                    {
                                        if (subkey.GetValue("DisplayName").ToString().Contains("Mount") && subkey.GetValue("DisplayName").ToString().Contains("Warband"))
                                        {
                                            string lpath = "";
                                            if (subkey.GetValue("UninstallString").ToString().Contains("steam.exe"))
                                            {
                                                lpath = GetSteamPath();
                                                installs.Add(new InstallPath(subkey.GetValue("DisplayName").ToString(), subkey.GetValue("InstallLocation").ToString(), "", false, lpath));
                                            }
                                            else if (File.Exists(subkey.GetValue("UninstallString").ToString().Replace("uninstall.exe", "mb_warband.exe"))) // if selected "Modules" folder
                                            {
                                                lpath = subkey.GetValue("UninstallString").ToString().Replace("uninstall.exe", "mb_warband.exe");
                                                installs.Add(new InstallPath(subkey.GetValue("DisplayName").ToString(), subkey.GetValue("UninstallString").ToString().Replace("uninstall.exe", "") + @"Modules\NordInvasion\", "", false, lpath));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return installs;
        }



        private void Help_Click(object sender, RoutedEventArgs e)
        {
            help = new OptionsHelp();
            help.Show();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (help != null)
            {
                help.Close();
            }
        }

        private void BrowseLauncher_Click(object sender, RoutedEventArgs e)
        {
            InstallPath selected = (InstallPath)data.CurrentCell.Item;

            NotificationWindow nw = new NotificationWindow("Steam_Check_Title",
                new List<NotificationWindowItem> {
                    new NotificationWindowItem("Steam_Check_Text")
                }, 3)
            {
                Owner = this
            };
            nw.ShowDialog();

            bool need2find = true;
            bool steam = false;
            if (nw.Result == 1)
            {
                steam = true;
                try
                {
                    var spath = GetSteamPath();
                    if (spath != "")
                    {
                        selected.Executable = spath;
                        need2find = false;
                    }
                    else
                    {
                        nw = new NotificationWindow("Steam_Find_Title",
                            new List<NotificationWindowItem> {
                                new NotificationWindowItem("Steam_Find_Text")
                            }, 0)
                        {
                            Owner = this
                        };
                        nw.ShowDialog();
                    }
                }
                catch
                {
                    //something went wrong with autofind - just continue as if all is well. Probably a permissions issue - we will use file finder instead.
                }
            }

            if (need2find)
            {
                System.Windows.Controls.Button btn = sender as System.Windows.Controls.Button;

                string initialPath = null;
                if (btn.Tag == null || (string)btn.Tag == "" || (string)btn.Tag == @"steam://rungameid/48700")
                {
                    if (selected.Path != null && selected.Path != "")
                    {
                        initialPath = selected.Path;
                    }
                    else
                    {
                        initialPath = @"C:\";
                    }
                }
                else
                {
                    initialPath = (string)btn.Tag;
                }

                FileInfo file = null;
                string dialogTitle = "";
                if (steam)
                {
                    dialogTitle = this.FindResource("Select_Steam") as string;
                }
                else
                {
                    dialogTitle = this.FindResource("Select_Warband") as string;
                }
                CommonOpenFileDialog dialog = new CommonOpenFileDialog
                {
                    InitialDirectory = initialPath,
                    IsFolderPicker = false,
                    Title = dialogTitle
                };
                dialog.Filters.Add(new CommonFileDialogFilter(this.FindResource("Executables") as string, "*.exe"));
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    file = new FileInfo(dialog.FileName);
                }

                if (file == null)
                {
                    this.Activate();
                    return;
                }

                selected.Executable = file.FullName;
            }

            //Sends the enter event - on a datagrid, this auto adds a new blank row. Generally, it just looks a bit better.
            this.data.CommitEdit();
            var key = Key.Enter;
            var target = Keyboard.FocusedElement;
            var routedEvent = Keyboard.KeyDownEvent;

            target.RaiseEvent(
              new System.Windows.Input.KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(this),
                0,
                key)
              { RoutedEvent = routedEvent }
            );

            this.data.CommitEdit();
            key = Key.Up;
            target = Keyboard.FocusedElement;
            routedEvent = Keyboard.KeyDownEvent;
            target.RaiseEvent(
              new System.Windows.Input.KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(this),
                0,
                key)
              { RoutedEvent = routedEvent }
            );
            this.Activate();

        }

        private string GetSteamPath()
        {
            List<String> registry_key = new List<string>
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            if (Environment.Is64BitOperatingSystem)
            {
                foreach (string reg in registry_key)
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (Microsoft.Win32.RegistryKey key = hklm.OpenSubKey(reg))
                    {
                        if (key != null)
                        {
                            foreach (string subkey_name in key.GetSubKeyNames())
                            {
                                using (RegistryKey subkey = key.OpenSubKey(subkey_name))
                                {
                                    if (subkey.GetValue("DisplayName") != null && subkey.GetValue("Publisher") != null)
                                    {
                                        if (subkey.GetValue("DisplayName").ToString() == "Steam" && subkey.GetValue("Publisher").ToString() == "Valve Corporation")
                                        {
                                            if (subkey.GetValue("UninstallString").ToString().Contains("uninstall.exe"))
                                            {
                                                return subkey.GetValue("UninstallString").ToString().Replace("uninstall.exe", "steam.exe");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (string reg in registry_key)
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    using (Microsoft.Win32.RegistryKey key = hklm.OpenSubKey(reg))
                    {
                        if (key != null)
                        {
                            foreach (string subkey_name in key.GetSubKeyNames())
                            {
                                using (RegistryKey subkey = key.OpenSubKey(subkey_name))
                                {
                                    if (subkey.GetValue("DisplayName") != null && subkey.GetValue("Publisher") != null)
                                    {
                                        if (subkey.GetValue("DisplayName").ToString() == "Steam" && subkey.GetValue("Publisher").ToString() == "Valve Corporation")
                                        {
                                            if (subkey.GetValue("UninstallString").ToString().Contains("uninstall.exe"))
                                            {
                                                return subkey.GetValue("UninstallString").ToString().Replace("uninstall.exe", "steam.exe");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return "";
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
