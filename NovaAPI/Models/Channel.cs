using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Models
{
    public class Channel
    {
        public string Table_Id { get; set; }
        public string Owner_UUID { get; set; }
        public bool IsGroup { get; set; }
        public string GroupName { get; set; }
        public string ChannelName { get; set; }
        public List<string> Members { get; set; }
    }
}
