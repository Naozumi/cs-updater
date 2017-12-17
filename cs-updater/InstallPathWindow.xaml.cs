using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace cs_updater
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class InstallPathWindow : Window
    {
        public InstallPathWindow(List<InstallPath> installs)
        {
            this.Installs = installs;
            InitializeComponent();
            data.ItemsSource = this.Installs;
            data.Items.Refresh();
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
            foreach(InstallPath install in Installs)
            {
                if (install.IsDefault) d++;
                if (!install.Path.EndsWith(@"\")) install.Path += @"\";
            }
            

            if (d == 1)
            {
                DialogResult = true;
                this.Close();
            }
            else if(d > 1)
            {
                System.Windows.Forms.MessageBox.Show("Sorry, you can only have one default.\n\nPlease set only one directory as default to continue.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Please set one of the directories as your default to continue.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }
    }
}
