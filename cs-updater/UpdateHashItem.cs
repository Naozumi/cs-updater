using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_updater
{
    class UpdateHashItem
    {
        public string Name { get; set; }
        public string Crc { get; set; }
        public List<UpdateHashItem> Files { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public string Path { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        public Boolean Downloaded { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        public int Attempts { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        public int Verified { get; set; }

        public UpdateHashItem()
        {
        }

        public UpdateHashItem(string name, string crc)
        {
            this.Name = name;
            this.Crc = crc;
        }

        public UpdateHashItem(string name, string crc, List<UpdateHashItem> files)
        {
            Name = name;
            Crc = crc;
            Files = files;
        }

        public UpdateHashItem(string name, List<UpdateHashItem> files)
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
                foreach (UpdateHashItem sub in this.Files)
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

        public List<UpdateHashItem> getFolders(String path)
        {
            var l = new List<UpdateHashItem>();
            if (this.Files == null) return null;
            foreach (UpdateHashItem f in this.Files)
            {
                if (f.isFolder())
                {
                    f.Path = path + f.Name;
                    l.Add(f);
                    f.getFolders(path + "\\");
                }
            }
            return l;
        }

        public List<UpdateHashItem> getFiles(String path)
        {
            var l = new List<UpdateHashItem>();
            if (this.Files == null) return null;
            foreach (UpdateHashItem f in this.Files)
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
