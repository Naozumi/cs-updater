using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_updater_lib
{
    [Serializable]
    public class UpdateHashItem
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
        public Boolean Verified { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        public Boolean Writable { get; set; }

        public UpdateHashItem()
        {
            this.Writable = true;
        }

        public UpdateHashItem(string name, string crc)
        {
            this.Name = name;
            this.Crc = crc;
            this.Writable = true;
        }

        public UpdateHashItem(string name, string crc, List<UpdateHashItem> files)
        {
            Name = name;
            Crc = crc;
            Files = files;
            this.Writable = true;
        }

        public UpdateHashItem(string name, List<UpdateHashItem> files)
        {
            this.Name = name;
            this.Files = files;
            this.Writable = true;
        }

        public double GetFilesCount()
        {
            double c = 0;
            if (this.Files != null)
            {
                c += this.Files.Count();
                foreach (UpdateHashItem sub in this.Files)
                {
                    c += sub.GetFilesCount();
                }
            }
            else
            {
                c = 0;
            }

            return c;
        }

        public bool IsFolder()
        {
            if (this.Crc == null)
            {
                return true;
            }
            return false;
        }

        public List<UpdateHashItem> GetFolders(String path)
        {
            var l = new List<UpdateHashItem>();
            if (this.Files == null) return null;
            foreach (UpdateHashItem f in this.Files)
            {
                if (f.IsFolder())
                {
                    f.Path = path + f.Name;
                    l.Add(f);
                    f.GetFolders(path + "\\");
                }
            }
            return l;
        }

        public List<UpdateHashItem> GetFiles(String path)
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
                    f.GetFiles(path + f.Name + "\\");
                }
            }
            return l;
        }
    }
}
