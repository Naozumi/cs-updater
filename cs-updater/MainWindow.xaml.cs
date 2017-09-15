using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace cs_updater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            

            DirectoryInfo dir = new DirectoryInfo("C:\\cstest\\");

            var jsonObject = BuildStructure(dir);

            string output = JsonConvert.SerializeObject(jsonObject, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            rtf_news.Document.Blocks.Clear();
            rtf_news.Document.Blocks.Add(new Paragraph(new Run(output)));


        }

        private static List<Node> BuildStructure(DirectoryInfo directory)
        {
            var jsonObject = new List<Node>();

            foreach (var file in directory.GetFiles())
            {
                var crc = string.Empty;
                using (FileStream stream = File.OpenRead(file.FullName))
                {
                    using (SHA1Managed sha = new SHA1Managed())
                    {
                        byte[] checksum = sha.ComputeHash(stream);
                        crc = BitConverter.ToString(checksum)
                            .Replace("-", string.Empty).ToLower();
                    }
                }
                jsonObject.Add(new Node("file", file.Name, crc));
            }

            foreach (var folder in directory.GetDirectories())
            {
                jsonObject.Add(new Node("folder", folder.Name, BuildStructure(new DirectoryInfo(folder.FullName))));
            }

            return jsonObject;
        }

    }
}
