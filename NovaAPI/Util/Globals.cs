namespace NovaAPI.Util
{
    public static class Globals
    {
        public static readonly string RootMedia = "Media";
        public static readonly string ChannelMedia = RootMedia + "/ChannelMedia";
        public static readonly string DefaultAvatarMedia = "DefaultAvatars";
        public static readonly string AvatarMedia = RootMedia + "/Avatars";

        public static readonly string[] ContentTypes = new string[] {"png", "jpeg", "jpg", "mp4", "mp3"};
    }
}