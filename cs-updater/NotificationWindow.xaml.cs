using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace cs_updater
{
    /// <summary>
    /// Interaction logic for NotificationWindow.xaml
    /// 0 = ok
    /// 1 = close
    /// 2 = ok/cancel
    /// 3 = yes/no
    /// </summary>
    public partial class NotificationWindow : Window
    {
        public int Result { get; private set; }

        public NotificationWindow(string title, List<NotificationWindowItem> items, int options = 0)
        {
            InitializeComponent();
            Result = 0;
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
                textblock.HorizontalAlignment = HorizontalAlignment.Center;
                textblock.TextWrapping = TextWrapping.Wrap;
                textblock.TextAlignment = TextAlignment.Center;
                TextArea.Children.Add(textblock);
            }

            switch (options){
                case 0:
                    Btn_1.SetResourceReference(System.Windows.Controls.Button.ContentProperty, "OK");
                    Btn_1.Visibility = Visibility.Visible;
                    Btn_2.Visibility = Visibility.Collapsed;
                    break;
                case 1:
                    Btn_1.SetResourceReference(System.Windows.Controls.Button.ContentProperty, "Close");
                    Btn_1.Visibility = Visibility.Visible;
                    Btn_2.Visibility = Visibility.Collapsed;
                    break;
                case 2:
                    Btn_1.SetResourceReference(System.Windows.Controls.Button.ContentProperty, "OK");
                    Btn_1.Visibility = Visibility.Visible;
                    Btn_2.SetResourceReference(System.Windows.Controls.Button.ContentProperty, "Cancel");
                    Btn_2.Visibility = Visibility.Visible;
                    break;
                case 3:
                    Btn_1.SetResourceReference(System.Windows.Controls.Button.ContentProperty, "Yes");
                    Btn_1.Visibility = Visibility.Visible;
                    Btn_2.SetResourceReference(System.Windows.Controls.Button.ContentProperty, "No");
                    Btn_2.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void Btn1_Click(object sender, RoutedEventArgs e)
        {
            this.Result = 1;
            this.Close();
        }

        private void Btn2_Click(object sender, RoutedEventArgs e)
        {
            this.Result = -1;
            this.Close();
        }
    }
}
