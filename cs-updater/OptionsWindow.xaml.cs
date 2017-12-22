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

namespace cs_updater
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        private OptionsHelp help = null;

        public OptionsWindow(List<InstallPath> installs)
        {
            this.Installs = installs;
            InitializeComponent();
            data.ItemsSource = this.Installs;
            data.Items.Refresh();
            cb_verify.IsChecked = Properties.Settings.Default.AutoVerify;
            cb_update.IsChecked = Properties.Settings.Default.AutoUpdate;
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
                Title = "Select the Warband Modules folder"
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
                DialogResult sure = System.Windows.Forms.MessageBox.Show("This does not appear to be the Modules or NordInvasion folder.\n\nAre you sure you want to download here?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (sure == System.Windows.Forms.DialogResult.No)
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


            //Sends the enter event - on a datagrid, this auto adds a new blank row. Generally, it just looks a bit better.
            InstallPath selected = (InstallPath)data.CurrentCell.Item;
            selected.Path = path;


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
                    System.Windows.Forms.MessageBox.Show("Woops - looks like you added a directory without a path.\n\nPlease ensure all paths are set, or delete any options you want don't want.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!install.Path.EndsWith(@"\")) install.Path += @"\";
            }


            if (Installs.Count == 0)
            {
                //No installs
                System.Windows.Forms.MessageBox.Show("Please set a directory to continue.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (d == 1)
            {
                // All OK
                DialogResult = true;
                Properties.Settings.Default.AutoVerify = (bool)cb_verify.IsChecked;
                Properties.Settings.Default.AutoUpdate = (bool)cb_update.IsChecked;
                Properties.Settings.Default.Save();
                this.Close();
            }
            else if (d > 1)
            {
                //Too many defaults
                System.Windows.Forms.MessageBox.Show("Sorry, you can only have one default.\n\nPlease set only one directory as default to continue.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }

        private void AutomaticallyAddInstalls(Object sender, RoutedEventArgs e)
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
                System.Windows.Forms.MessageBox.Show(newInstalls.Count + " new installs found and added to the list.", newInstalls.Count + " installs found.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Installs.AddRange(foundInstalls);
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("No new installs found.", newInstalls.Count + " new installs found.", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            data.Items.Refresh();
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
                                            installs.Add(new InstallPath(subkey.GetValue("DisplayName").ToString(), subkey.GetValue("InstallLocation").ToString() + @"\Modules\NordInvasion\", "", false));
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
                                            installs.Add(new InstallPath(subkey.GetValue("DisplayName").ToString(), subkey.GetValue("InstallLocation").ToString(), "", false));
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

        private void Help_Click(object sender, MouseButtonEventArgs e)
        {
            if (help == null)
            {
                help = new OptionsHelp();
            }
            help.Show();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (help != null)
            {
                help.Close();
            }
        }
    }
}
