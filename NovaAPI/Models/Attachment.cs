using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Models
{
    public class Attachment
    {
        public string ContentUrl { get; set; }
        public string Filename { get; set; }
        public string MimeType { get; set; }
        public int Size { get; set; }
        public int ContentWidth { get; set; }
        public int ContentHeight { get; set; }
        public Dictionary<string, string> Keys { get; set; }
        public string IV { get; set; }
    }
}
