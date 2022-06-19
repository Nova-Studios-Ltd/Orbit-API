using System;
using System.ComponentModel;
using MySql.Data.MySqlClient;
using NovaAPI.Controllers;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace NovaAPI.Util
{
    public static class GlobalUtils
    {
        public static readonly string RootMedia = "Media";
        public static readonly string RootDebug = "Debug";
        public static readonly string ChannelMedia = RootMedia + "/ChannelMedia";
        public static readonly string DefaultAvatarMedia = "DefaultAvatars";
        public static readonly string AvatarMedia = RootMedia + "/Avatars";
        public static readonly string ChannelAvatarMedia = RootMedia + "/ChannelAvatars";

        public static readonly string[] ContentTypes = new string[] {"png", "jpeg", "jpg", "mp4", "mp3"};
        public static readonly Mutex ClientSync = new();

        public static void RemoveAttachmentContent(NovaChatDatabaseContext context, string channel_uuid, string[] content_uuids)
        {
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Master);
            conn.Open();
            foreach (string file in content_uuids)
            {
                using MySqlCommand removeAttachment = new("DELETE FROM ChannelMedia WHERE (File_UUID=@uuid)", conn);
                removeAttachment.Parameters.AddWithValue("@uuid", file);
                removeAttachment.ExecuteNonQuery();
                if (File.Exists(Path.Combine(ChannelMedia, channel_uuid, file))) File.Delete(Path.Combine(ChannelMedia, channel_uuid, file));
            }
        }
        
        public static string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }
        
        public static string GetDescription(this Enum e)
        {
            var attribute =
                e.GetType()
                        .GetTypeInfo()
                        .GetMember(e.ToString())
                        .FirstOrDefault(member => member.MemberType == MemberTypes.Field)
                        .GetCustomAttributes(typeof(DescriptionAttribute), false)
                        .SingleOrDefault()
                    as DescriptionAttribute;

            return attribute?.Description ?? e.ToString();
        }
    }
}