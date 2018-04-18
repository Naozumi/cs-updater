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
        public NotificationWindow(string title, List<NotificationWindowItem> items)
        {
            InitializeComponent();
            LocUtil.SetDefaultLanguage(this);

            string ti = this.FindResource(title) as string;
            this.Title = ti;

            foreach (NotificationWindowItem item in items)
            {
                var textblock = new TextBlock();
                if (item.reference)
                {
                    textblock.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, item.text);
                }
                else
                {
                    textblock.Text = item.text;
                }
                
                textblock.TextWrapping = TextWrapping.Wrap;
                TextArea.Children.Add(textblock);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
