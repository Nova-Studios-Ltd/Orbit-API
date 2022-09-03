using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NovaAPI.Controllers;

namespace NovaAPI.Models
{
    public class Channel
    {
        public string Table_Id { get; set; }
        public string Owner_UUID { get; set; }
        public ChannelTypes ChannelType { get; set; }
        public string ChannelName { get; set; }
        public string ChannelIcon { get; set; }
        public List<string> Members { get; set; }
    }
}
