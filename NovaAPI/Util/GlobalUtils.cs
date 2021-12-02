using MySql.Data.MySqlClient;
using NovaAPI.Controllers;
using System.IO;

namespace NovaAPI.Util
{
    public static class GlobalUtils
    {
        public static readonly string RootMedia = "Media";
        public static readonly string ChannelMedia = RootMedia + "/ChannelMedia";
        public static readonly string DefaultAvatarMedia = "DefaultAvatars";
        public static readonly string AvatarMedia = RootMedia + "/Avatars";

        public static readonly string[] ContentTypes = new string[] {"png", "jpeg", "jpg", "mp4", "mp3"};

        public static void RemoveAttachmentContent(NovaChatDatabaseContext context, string channel_uuid, string[] content_uuids)
        {
            using MySqlConnection conn = context.GetChannels();
            foreach (string file in content_uuids)
            {
                using MySqlCommand removeAttachment = new("DELETE FROM ChannelMedia WHERE (File_UUID=@uuid)", conn);
                removeAttachment.Parameters.AddWithValue("@uuid", file);
                removeAttachment.ExecuteNonQuery();
                if (File.Exists(Path.Combine(ChannelMedia, channel_uuid, file))) File.Delete(Path.Combine(ChannelMedia, channel_uuid, file));
            }
        }
    }
}