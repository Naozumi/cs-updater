using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Forms;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Threading;
using System.Threading.Tasks;


namespace cs_updater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        UpdateHash hashObject = new UpdateHash();

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

        }

        private UpdateHash BuildStructure(DirectoryInfo directory)
        {
            var hash = new UpdateHash
            {
                Module = "NordInvasion",
                Files = BuildFileStructure(directory)
            };

            return hash;
        }

        private static List<UpdateHashFiles> BuildFileStructure(DirectoryInfo directory)
        {
            var jsonObject = new List<UpdateHashFiles>();

            try
            {
                foreach (var file in directory.GetFiles())
                {
                    var crc = string.Empty;
                    {
                        using (FileStream stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) // File.OpenRead(file.FullName))
                        {
                            using (SHA1Managed sha = new SHA1Managed())
                            {
                                byte[] checksum = sha.ComputeHash(stream);
                                crc = BitConverter.ToString(checksum)
                                    .Replace("-", string.Empty).ToLower();
                            }
                        }
                        jsonObject.Add(new UpdateHashFiles(file.Name, crc));
                    }
                }
            }
            catch (Exception err)
            {
                System.Windows.Forms.MessageBox.Show("Unable to complete building of JSON file due to error: \n\n" + err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            foreach (var folder in directory.GetDirectories())
            {
                jsonObject.Add(new UpdateHashFiles(folder.Name, BuildFileStructure(new DirectoryInfo(folder.FullName))));
            }

            return jsonObject;
        }

        private async void Modules_Build_Click(object sender, RoutedEventArgs e)
        {
            //note: currently this button just does "useful" features required in/to prove the final version.

            //set the directory
            DirectoryInfo dir = null;

            CommonOpenFileDialog dialog = new CommonOpenFileDialog
            {
                InitialDirectory = "C:\\",
                IsFolderPicker = true
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                dir = new DirectoryInfo(dialog.FileName);
            }

            if (dir == null) return;

            //build the json Object which contains all the files and folders
            hashObject = await Task.Run(() => BuildStructure(dir));
            hashObject.Source = dir.FullName;
            if (hashObject.Files == null) return;

            //convert the json object to a json string
            string jobjString = await Task.Run(() => JsonConvert.SerializeObject(hashObject, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));

            //display the json file in the web browser window.
            string output = "<html><body oncontextmenu='return false; '><pre>" + jobjString + "</pre></body</html>";
            web_news.NavigateToString(output);
        }

        private void Modules_Child_Count_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.MessageBox.Show("Files & Folders found: " + hashObject.getFileCount().ToString(), "F&F Count");
        }

        private void Modules_Compress_Click(object sender, RoutedEventArgs e)
        {
            if (hashObject.Source == null) return;

            //set the directory
            DirectoryInfo dir = null;

            CommonOpenFileDialog dialog = new CommonOpenFileDialog
            {
                InitialDirectory = "C:\\",
                IsFolderPicker = true
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                dir = new DirectoryInfo(dialog.FileName);
            }

            if (dir == null) return;

            CompressFiles(dir.FullName, null, hashObject.Files);
        }

        private void CompressFiles(string baseDirectory, string subDirectory, List<UpdateHashFiles> files)
        {
            if (!baseDirectory.EndsWith("\\")) baseDirectory += "\\";
            if (subDirectory!=null && !subDirectory.EndsWith("\\")) subDirectory += "\\";

            if (files == null) return;
            foreach (var f in files)
            {
                if (f.isFolder())
                {
                    Directory.CreateDirectory(baseDirectory + subDirectory + f.Name);
                    if (f.Files != null) CompressFiles(baseDirectory, subDirectory + f.Name + "\\", f.Files);
                }
                else
                {
                    CreateTarGZ(baseDirectory + subDirectory + f.Name, hashObject.Source + "\\" + subDirectory + f.Name);
                }
            }
        }

        private void CreateTarGZ(string tgzFilename, string fileName)
        {
            using (var outStream = File.Create(tgzFilename + ".tgz"))
            using (var gzoStream = new GZipOutputStream(outStream))
            using (var tarArchive = TarArchive.CreateOutputTarArchive(gzoStream))
            {
                gzoStream.SetLevel(9);
                tarArchive.RootPath = System.IO.Path.GetDirectoryName(fileName);

                var tarEntry = TarEntry.CreateEntryFromFile(fileName);
                tarEntry.Name = System.IO.Path.GetFileName(fileName);

                tarArchive.WriteEntry(tarEntry, true);
            }
        }
    }
}
