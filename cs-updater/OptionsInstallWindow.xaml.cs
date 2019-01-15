using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using cs_updater_lib;

namespace cs_updater
{
    /// <summary>
    /// Interaction logic for OptionsInstallWindow.xaml
    /// </summary>
    public partial class OptionsInstallWindow : Window
    {
        public InstallPath install;

        public OptionsInstallWindow(InstallPath installPath)
        {
            InitializeComponent();
            if (install != null)
            {
                if (installPath.Name != null) Tb_Name.Text = installPath.Name;
                if (installPath.Path != null) Tb_Folder.Text = installPath.Path;
                if (installPath.Executable != null) Tb_Launcher.Text = installPath.Executable;
                if (installPath.Password != null) Tb_Password.Text = installPath.Password;
            }
        }

        private void Btn_Folder_Click(object sender, RoutedEventArgs e)
        {
            string initialPath = null;
            if (Tb_Folder.Text == null || (string)Tb_Folder.Text == "")
            {
                initialPath = @"C:\";
            }
            else
            {
                initialPath = (string)Tb_Folder.Text;
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
                    return;
                }
            }

            if (dir.FullName.ToUpper().EndsWith(@"\MODULES"))
            {
                Tb_Folder.Text = dir.FullName + @"\NordInvasion";
            }
            else
            {
                Tb_Folder.Text = dir.FullName;
            }
        }

        private void Btn_Launcher_Click(object sender, RoutedEventArgs e)
        {

            string selected = (string)Tb_Launcher.Text;

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
                        Tb_Launcher.Text = spath;
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
                if (Tb_Launcher.Text == null || (string)Tb_Launcher.Text == "" || (string)Tb_Launcher.Text == @"steam://rungameid/48700")
                {

                    initialPath = Tb_Launcher.Text;
                }
                else
                {
                    initialPath = @"C:\";
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
                    return;
                }

                Tb_Launcher.Text = file.FullName;
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

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            install = new InstallPath(Tb_Name.Text, Tb_Folder.Text, Tb_Password.Text, false, Tb_Launcher.Text);
        }
    }
}
