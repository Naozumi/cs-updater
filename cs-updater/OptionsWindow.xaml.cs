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
using System.Globalization;

namespace cs_updater
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public OptionsWindow(List<InstallPath> installs)
        {
            this.Installs = installs;
            InitializeComponent();
            LocUtil.SetDefaultLanguage(this);
            dataList.ItemsSource = this.Installs;
            dataList.Items.Refresh();
            cb_verify.IsChecked = Properties.Settings.Default.AutoVerify;
            cb_update.IsChecked = Properties.Settings.Default.AutoUpdate;
            tb_threads_ch.Text = Properties.Settings.Default.Threads_Check.ToString();
            tb_threads_dl.Text = Properties.Settings.Default.Threads_Download.ToString();
        }

        public List<InstallPath> Installs { get; set; }

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

        private void Add_Installation(object sender, RoutedEventArgs e)
        {
            InstallPath newInstallPath = new InstallPath();
            OptionsEditorWindow installWindow = new OptionsEditorWindow(newInstallPath);
            installWindow.Left = this.Left + (this.Width - installWindow.Width) / 2;
            installWindow.Top = this.Top + (this.Height - installWindow.Height) / 2;
            installWindow.ShowDialog();

            if ((bool)installWindow.DialogResult)
            {
                Installs.Add(installWindow.GetInstall());
                dataList.Items.Refresh();
            }
            newInstallPath = null;
            installWindow = null;
        }

        private void Edit_Installation(object sender, RoutedEventArgs e)
        {
            if (dataList.SelectedIndex < 0) return;
            InstallPath editInstallPath = Installs[dataList.SelectedIndex];
            OptionsEditorWindow installWindow = new OptionsEditorWindow(editInstallPath);
            installWindow.Left = this.Left + (this.Width - installWindow.Width) / 2;
            installWindow.Top = this.Top + (this.Height - installWindow.Height) / 2;
            installWindow.ShowDialog();

            if ((bool)installWindow.DialogResult)
            {
                Installs[dataList.SelectedIndex] = installWindow.GetInstall();
            }
            editInstallPath = null;
            installWindow = null;
            dataList.Items.Refresh();
        }

        private void Set_Default_Installation(object sender, RoutedEventArgs e)
        {
            if (dataList.SelectedIndex < 0) return;
            InstallPath activeInstallPath = Installs[dataList.SelectedIndex];
            foreach (InstallPath i in Installs)
            {
                i.IsDefault = false;
            }
            activeInstallPath.IsDefault = true;
            dataList.Items.Refresh();
        }

        private void Delete_Installation(object sender, RoutedEventArgs e)
        {
            if (dataList.SelectedIndex < 0) return;
            Installs.RemoveAt(dataList.SelectedIndex);
            dataList.Items.Refresh();
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
            dataList.Items.Refresh();
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

        public object ConvertVisibility(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = false;
            if (value is bool)
            {
                flag = (bool)value;
            }
            else if (value is bool?)
            {
                bool? nullable = (bool?)value;
                flag = nullable.HasValue ? nullable.Value : false;
            }
            return (flag ? Visibility.Visible : Visibility.Hidden);
        }
    }
}
