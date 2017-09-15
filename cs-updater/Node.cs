using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_updater
{
    class Node
    {
        public string type { get; set; }
        public string name { get; set; }
        public string crc { get; set; }
        public List<Node> children { get; set; }

        public Node(string type, string name, string crc)
        {
            this.type = type;
            this.name = name;
            this.crc = crc;
        }

        public Node(string type, string name, List<Node> children)
        {
            this.type = type;
            this.name = name;
            this.children = children;
        }
    }
}
