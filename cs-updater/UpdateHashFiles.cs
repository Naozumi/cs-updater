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

        public UpdateHashFiles(string name, string crc)
        {
            this.Name = name;
            this.Crc = crc;
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
    }
}
