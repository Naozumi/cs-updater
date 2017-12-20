using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_updater_lib
{
    public class HostServer
    {
        public string Url { get; set; }
        public long Time { get; set; }
        public UpdateHash Json { get; set; }
        public bool Working { get; set; }
        public bool InvalidPassword { get; set; }
        public Exception DownloadException { get; set; }
    }
}
