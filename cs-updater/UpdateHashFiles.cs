using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_updater
{
    class UpdateHashFiles
    {
        public string Name { get; set; }
        public string Crc { get; set; }
        public List<UpdateHashFiles> Files { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        public string Path { get; set; }

        public UpdateHashFiles()
        {
        }

        public UpdateHashFiles(string name, string crc)
        {
            this.Name = name;
            this.Crc = crc;
        }

        public UpdateHashFiles(string name, string crc, List<UpdateHashFiles> files)
        {
            Name = name;
            Crc = crc;
            Files = files;
        }

        public UpdateHashFiles(string name, List<UpdateHashFiles> files)
        {
            this.Name = name;
            this.Files = files;
        }

        public int getFilesCount()
        {
            int c = 0;
            if (this.Files != null)
            {
                c += this.Files.Count();
                foreach (UpdateHashFiles sub in this.Files)
                {
                    c += sub.getFilesCount();
                }
            }
            else
            {
                c = 0;
            }

            return c;
        }

        public bool isFolder()
        {
            if (this.Crc == null)
            {
                return true;
            }
            return false;
        }

        public List<UpdateHashFiles> getFolders(String path)
        {
            var l = new List<UpdateHashFiles>();
            if (this.Files == null) return null;
            foreach (UpdateHashFiles f in this.Files)
            {
                if (f.isFolder())
                {
                    f.Path = path;
                    l.Add(f);
                    f.getFolders(path + "\\" + f.Name + "\\");
                }
            }
            return l;
        }

        public List<UpdateHashFiles> getFiles(String path)
        {
            var l = new List<UpdateHashFiles>();
            if (this.Files == null) return null;
            foreach (UpdateHashFiles f in this.Files)
            {
                if (f.Crc != null)
                {
                    f.Path = path + f.Name;
                    l.Add(f);
                }
                else
                {
                    f.getFiles(path + f.Name + "\\");
                }
            }
            return l;
        }
    }
}
