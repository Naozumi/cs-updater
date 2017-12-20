using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_updater_lib
{
    [Serializable]
    public class UpdateHash
    {
        public string Module { get; set; }
        public string ModuleVersion { get; set; }
        public string UpdaterVersion { get; set; }
        public Boolean SelfUpdate { get; set; }
        public string Host { get; set; }
        public List<UpdateHashItem> Files { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        public string Source { get; set; }

        public UpdateHash()
        {
        }

        public UpdateHash(string module, string moduleVersion, string updaterVersion, string host, List<UpdateHashItem> files, string source)
        {
            Module = module;
            ModuleVersion = moduleVersion;
            UpdaterVersion = updaterVersion;
            Host = host;
            Files = files;
            Source = source;
        }

        public int getFileCount()
        {
            if (this.Files == null) return 0;
            int count = this.Files.Count();
            foreach (UpdateHashItem i in this.Files)
            {
                count += i.getFilesCount();
            }
            return count;
        }

        public List<UpdateHashItem> getFolders()
        {
            var l = new List<UpdateHashItem>();
            if (this.Files == null) return null;
            foreach (UpdateHashItem f in this.Files)
            {
                if (f.isFolder())
                {
                    f.Path = f.Name;
                    l.Add(f);
                    l.AddRange(f.getFolders(f.Path + "\\"));
                }
            }
            return l;
        }

        public List<UpdateHashItem> getFiles()
        {
            var l = new List<UpdateHashItem>();
            if (this.Files == null) return null;
            foreach (UpdateHashItem f in this.Files)
            {
                if (!f.isFolder())
                {
                    f.Path = f.Name;
                    l.Add(f);
                }
                else {
                    l.AddRange(f.getFiles(f.Name + "\\"));
                }
            }
            return l;
        }
    }
}
