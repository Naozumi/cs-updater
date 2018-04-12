using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace cs_updater
{
    /// <summary>
    /// Interaction logic for NotificationWindow.xaml
    /// </summary>
    public partial class NotificationWindow : Window
    {
        public NotificationWindow(string text)
        {
            InitializeComponent();
            this.text.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, text);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
