using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Models
{
    public class ChannelMessage
    {
        public string Message_Id { get; set; }
        public string Author { get; set; }
        public string Author_UUID { get; set; }
        public string Content { get; set; }
        public string IV { get; set; }
        public Dictionary<string, string> EncryptedKeys { get; set; }
        public List<Attachment> Attachments { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime EditedTimestamp { get; set; }
        public bool Edited { get; set; }
        public string Avatar { get; set; }
    }
}
