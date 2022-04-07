using System.IO;
using NovaAPI.Interfaces;

namespace NovaAPI.DataTypes
{
    public class MediaFile
    {
        public Stream File { get; private set; }
        public IMeta Meta { get; private set; }

        public MediaFile(Stream file, IMeta meta)
        {
            File = file;
            Meta = meta;
        }
    }
}