using NovaAPI.Interfaces;

namespace NovaAPI.DataTypes
{
    public class IconMeta : IMeta
    {
        public string Filename { get; set; }
        public string MimeType { get; set; }
        public long Filesize { get; set; }
        public string Channel_UUID { get; set; }

        public IconMeta(string filename, long filesize, string channelUuid)
        {
            Filename = filename;
            Filesize = filesize;
            Channel_UUID = channelUuid;
            MimeType = "image/png";
        }
    }
}