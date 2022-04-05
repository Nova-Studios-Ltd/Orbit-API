using NovaAPI.Interfaces;

namespace NovaAPI.DataTypes
{
    public class AvatarMeta : IMeta
    {
        public string Filename { get; set; }
        public string MimeType { get; set; }
        public long Filesize { get; set; }
        public string User_UUID { get; set; }

        public AvatarMeta(string filename, long filesize, string userUuid)
        {
            Filename = filename;
            MimeType = "image/png";
            Filesize = filesize;
            User_UUID = userUuid;
        }
    }
}