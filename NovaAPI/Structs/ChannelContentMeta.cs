using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Structs
{
    public class ChannelContentMeta
    {
        public int Width;
        public int Height;
        public string MimeType;

        public ChannelContentMeta(int width, int height, string mimeType)
        {
            Width = width;
            Height = height;
            MimeType = mimeType;
        }
    }
}
