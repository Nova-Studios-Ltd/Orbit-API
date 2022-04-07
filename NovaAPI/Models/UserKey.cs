using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Models
{
    public class UserKey
    {
        public string Priv { get; set; }
        public string PrivIV { get; set; }
        public string Pub { get; set; }
    }
}
