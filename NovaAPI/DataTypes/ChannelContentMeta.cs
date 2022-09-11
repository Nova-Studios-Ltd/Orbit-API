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
        public string User_UUID;
        
        public string MimeType { get; set; }
        public string Filename { get; set; }
        public long Filesize { get; set; }
        public string Keys { get; set; }
        public string IV { get; set; }

        public ChannelContentMeta(int width, int height, string mimeType, string filename, string channel_uuid, string user_uuid, long filesize, string keys, string iv)
        {
            Width = width;
            Height = height;
            MimeType = mimeType;
            Filename = filename;
            Channel_UUID = channel_uuid;
            User_UUID = user_uuid;
            Filesize = filesize;
            Keys = keys;
            IV = iv;
        }
    }
}
