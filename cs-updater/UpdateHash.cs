using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_updater
{
    class UpdateHash
    {
        public string Module { get; set; }
        public string ModuleVersion { get; set; }
        public string UpdaterVersion { get; set; }
        public string Host { get; set; }
        public List<UpdateHashFiles> Files { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        public string Source { get; set; }

        public int getFileCount()
        {
            if (this.Files == null) return 0;
            int count = this.Files.Count();
            foreach (UpdateHashFiles i in this.Files)
            {
                count += i.getFilesCount();
            }
            return count;
        }
    }
}
