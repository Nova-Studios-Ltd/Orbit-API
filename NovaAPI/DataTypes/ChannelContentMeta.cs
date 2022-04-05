using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NovaAPI.Interfaces;

namespace NovaAPI.DataTypes
{
    public class ChannelContentMeta : IMeta
    {
        public int Width;
        public int Height;
        public string Channel_UUID;
        
        public string MimeType { get; set; }
        public string Filename { get; set; }
        public long Filesize { get; set; }

        public ChannelContentMeta(int width, int height, string mimeType, string filename, string channel_uuid, long filesize)
        {
            Width = width;
            Height = height;
            MimeType = mimeType;
            Filename = filename;
            Channel_UUID = channel_uuid;
            Filesize = filesize;
        }
    }
}
