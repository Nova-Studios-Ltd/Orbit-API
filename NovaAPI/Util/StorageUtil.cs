using Microsoft.Extensions.Configuration;
using NovaAPI.Controllers;
using NovaAPI.Structs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Util
{
    public static class StorageUtil
    {
        public static string NC3Storage = "";
        public static string UserData = "";
        public static string ChannelData = "";
        public static string ChannelContent = "";
        public static string ChannelIcon = "";

        private static NovaChatDatabaseContext Context;

        public enum MediaType { Avatar, ChannelIcon, ChannelContent }
        public static void InitStorage(string directory, IConfigurationRoot config)
        {
            Context = new NovaChatDatabaseContext(config);

            NC3Storage = Path.Combine(directory, "NC3Storage");
            Console.ForegroundColor = ConsoleColor.Cyan;
            if (!Directory.Exists(NC3Storage))
            {
                Directory.CreateDirectory(NC3Storage);
                Console.WriteLine($"Storage ({NC3Storage}) Directory Created");
            }
            else Console.WriteLine($"Found Storage ({NC3Storage}) Directory. Continuing...");

            UserData = Path.Combine(NC3Storage, "UserData");
            if (!Directory.Exists(UserData))
            {
                Directory.CreateDirectory(UserData);
                Console.WriteLine($"UserData ({UserData}) Directory Created");
            }
            else Console.WriteLine($"Found UserData ({UserData}) Directory. Continuing...");

            ChannelData = Path.Combine(NC3Storage, "ChannelData");
            if (!Directory.Exists(ChannelData))
            {
                Directory.CreateDirectory(ChannelData);
                Console.WriteLine($"ChannelData ({ChannelData}) Directory Created");
            }
            else Console.WriteLine($"Found ChannelData ({ChannelData}) Directory. Continuing...");

            ChannelContent = Path.Combine(ChannelData, "ChannelContent");
            if (!Directory.Exists(ChannelContent))
            {
                Directory.CreateDirectory(ChannelContent);
                Console.WriteLine($"ChannelContent ({ChannelContent}) Directory Created");
            }
            else Console.WriteLine($"Found ChannelContent ({ChannelContent}) Directory. Continuing...");

            ChannelIcon = Path.Combine(ChannelData, "ChannelIcon");
            if (!Directory.Exists(ChannelIcon))
            {
                Directory.CreateDirectory(ChannelIcon);
                Console.WriteLine($"ChannelIcon ({ChannelIcon}) Directory Created");
            }
            else Console.WriteLine($"Found ChannelIcon ({ChannelIcon}) Directory. Continuing...");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Data Directory Setup Complete");
        }
        
        public static void StoreFile(MediaType mediaType, Stream file, ChannelContentMeta meta = null)
        {
            if (mediaType == MediaType.ChannelContent)
            {

            }
            else if (mediaType == MediaType.Avatar)
            {

            }
            else
            {

            }
        }
    }
}
