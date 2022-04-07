namespace NovaAPI.Interfaces
{
    public interface IMeta
    {
        public string Filename { get; set; }
        public string MimeType { get; set; }
        public long Filesize { get; set; }
    }
}