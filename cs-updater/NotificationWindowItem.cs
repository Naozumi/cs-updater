using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_updater
{
    public class NotificationWindowItem
    {
        public String text { get; set; }
        public Boolean reference { get; set; }

        public NotificationWindowItem(string text, bool reference)
        {
            this.text = text;
            this.reference = reference;
        }
        public NotificationWindowItem(string text)
        {
            this.text = text;
            this.reference = true;
        }
    }
}
