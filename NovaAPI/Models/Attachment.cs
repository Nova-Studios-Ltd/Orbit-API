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
        public int Size { get; set; }
    }
}
