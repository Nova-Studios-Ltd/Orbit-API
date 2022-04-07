using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Models
{
    public class SentMessage
    {
        public string Content { get; set; }
        public string IV { get; set; }
        public Dictionary<string, string> EncryptedKeys { get; set; }
        public List<string> Attachments { get; set; } 
    }
}
