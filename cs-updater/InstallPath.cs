using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_updater
{
    public class InstallPath
    {
        public String Name { get; set; }
        public String Path { get; set; }
        public String Password { get; set; }
        public Boolean IsDefault { get; set; }

        public InstallPath() { }

        public InstallPath(String Name, String Path, String Password, Boolean IsDefault)
        {
            this.Name = Name;
            this.Path = Path;
            this.Password = Password;
            this.IsDefault = IsDefault;
        }
    }
}
