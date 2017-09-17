using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

            web_news.NavigateToString(BlankWebpage());
        }

        private string BlankWebpage()
        {
            return "<html><head><style>html{background-color:'#fff'}</style></head><body oncontextmenu='return false; '></body></html>";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //note: currently this button just does "useful" features required in/to prove the final version.

            //set the directory
            DirectoryInfo dir = new DirectoryInfo("C:\\cstest2\\");

            //build the json Object which contains all the files and folders
            List<Node> jsonObject = BuildStructure(dir);

            //count the number of items
            int count = jsonObject.Count();
            foreach (Node i in jsonObject)
            {
                count += i.getChildrenCount();
            }
            
            //convert the json object to a json string
            string jobjString = JsonConvert.SerializeObject(jsonObject, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            //convert the json string to an object
            dynamic jObj = JsonConvert.DeserializeObject(jobjString);

            //count the number of items in the deserialised string
            int c2 = jsonObject.Count();
            foreach (Node i in jsonObject)
            {
                c2 += i.getChildrenCount();
            }

            //display the json file in the web browser window.
            string output = "<html><body oncontextmenu='return false; '><pre>" + jobjString + "</pre></body</html>";
            web_news.NavigateToString(output);
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
