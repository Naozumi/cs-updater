using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using cs_updater_lib;
using Newtonsoft.Json;

namespace updater_generator
{
    class Program
    {

        static void Main(string[] args)
        {
            var options = new Options();
            if (args.Length != 3 && args.Length != 4)
            {
                Console.WriteLine("Please enter commands in the format: Input directory, Output directory, Version, Json File [optional]");
            }
            else
            {
                options.Input = args[0];
                options.Output = args[1];
                options.Version = args[2];
                if (args.Length == 3)
                {
                    options.Json = args[2] + ".json";
                }
                else
                {
                    options.Json = args[3];
                }

                //check input folder
                if (!System.IO.Path.IsPathRooted(options.Input))
                {
                    options.Input = Path.Combine(Directory.GetCurrentDirectory(), options.Input);
                }
                if (!Directory.Exists(options.Input))
                {
                    Console.WriteLine("Can't find input folder: " + options.Input);
                    return;
                }

                //check output folder
                if (!System.IO.Path.IsPathRooted(options.Output))
                {
                    options.Output = Path.Combine(Directory.GetCurrentDirectory(), options.Output);
                }
                if (!Directory.Exists(options.Output))
                {
                    Console.WriteLine("Can't find output folder:" + options.Output);
                    return;
                }

                //check json file
                if (!System.IO.Path.IsPathRooted(options.Json))
                {
                    options.Json = Path.Combine(Directory.GetCurrentDirectory(), options.Json);
                }

                HashAndCompress(options);
            }
        }

        private static void HashAndCompress(Options options)
        {
            //try
            //{
                Console.Write("Building object of all files... ");
                DirectoryInfo dir = new DirectoryInfo(options.Input);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Done");
                Console.ResetColor();

                Console.Write("Creating the JSON... ");
                UpdateHash hashObject = BuildStructure(dir);
                hashObject.ModuleVersion = options.Version;
                string jsonString = JsonConvert.SerializeObject(hashObject, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                System.IO.File.WriteAllText(options.Json, jsonString);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Done");
                Console.ResetColor();

                
                Console.Write("Compressing files... ");
                using (var progress = new ProgressBar())
                {
                    CompressFiles(options.Output, "", hashObject.Files, options.Input, progress, hashObject.getFileCount());
                }
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Done");
                Console.ResetColor();
            //}
            //catch (Exception ex)
            //{
            //    Console.ForegroundColor = ConsoleColor.Red;
            //    Console.WriteLine(ex.Message);
            //    Console.ResetColor();
            //}
        }

        private static UpdateHash BuildStructure(DirectoryInfo directory)
        {
            var hash = new UpdateHash
            {
                Module = "NordInvasion",
                Files = BuildFileStructure(directory)
            };

            return hash;
        }

        private static List<UpdateHashItem> BuildFileStructure(DirectoryInfo directory)
        {
            var jsonObject = new List<UpdateHashItem>();

            try
            {
                foreach (var file in directory.GetFiles())
                {
                    if (!file.Name.StartsWith("."))
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
                            jsonObject.Add(new UpdateHashItem(file.Name, crc));
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine("Unable to complete building of JSON file due to error: \n\n" + err.Message);
                return null;
            }

            foreach (var folder in directory.GetDirectories())
            {
                if (!folder.Name.StartsWith("."))
                {
                    jsonObject.Add(new UpdateHashItem(folder.Name, BuildFileStructure(new DirectoryInfo(folder.FullName))));
                }
            }

            return jsonObject;
        }

        private static double CompressFiles(string outputDirectory, string subDirectory, List<UpdateHashItem> files, string path, ProgressBar progress, double fileCount, double count = 0)
        {
            if (files == null) return count;
            foreach (var f in files)
            {
                if (f.isFolder())
                {
                    if (f.Files != null) count = CompressFiles(outputDirectory, Path.Combine(subDirectory, f.Name), f.Files, path, progress, fileCount, count);
                }
                else
                {
                    count++;
                    progress.Report((double)count / fileCount);
                    CreateGz(Path.Combine(path, subDirectory, f.Name), Path.Combine(outputDirectory, f.Crc));
                }
            }
            return count;
        }

        private static void CreateGz(String inputFile, String outputFile)
        {
            using (FileStream originalFileStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (FileStream compressedFileStream = File.Create(Path.Combine(outputFile + ".gz")))
            using (GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress))
            {
                originalFileStream.CopyTo(compressionStream);
            }
        }
    }
}
