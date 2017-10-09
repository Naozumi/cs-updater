using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_updater
{
    class Installs
    {
        public String Name { get; set; }
        public String Path { get; set; }

        public Installs() { }

        public Installs(String Name, String Path)
        {
            this.Name = Name;
            this.Path = Path;
        }
    }
}
