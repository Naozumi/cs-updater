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
    public partial class OptionsEditorWindow : Window
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public OptionsEditorWindow(InstallPath install)
        {
            this.tb_name.Text = install.Name;
            this.tb_path.Text = install.Path;
            this.tb_executable.Text = install.Executable;
            this.tb_password.Text = install.Password;
        }

        public InstallPath Install { get; set; }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close();
        }

        private void Path_Click(object sender, RoutedEventArgs e)
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
            tb_path.Text = path;
            this.Install.Path = path;
        }

        private void Executable_Click(object sender, RoutedEventArgs e)
        {

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
                        tb_executable.Text = spath;
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
                    if (tb_executable.Text != null && tb_executable.Text != "")
                    {
                        if (Directory.Exists(tb_executable.Text))
                        {
                            initialPath = tb_executable.Text;
                        }
                        else
                        {
                            initialPath = @"C:\";
                        }
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

                tb_executable.Text = file.FullName;
            }
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
