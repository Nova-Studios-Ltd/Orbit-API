using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Util
{
    public static class StorageUtil
    {
        public static void InitStorage(string directory)
        {
            string nc3Storage = Path.Combine(directory, "NC3Storage");
            Console.ForegroundColor = ConsoleColor.Cyan;
            if (!Directory.Exists(nc3Storage))
            {
                Directory.CreateDirectory(nc3Storage);
                Console.WriteLine("Storage Directory Created");
            }
            else Console.WriteLine("Found Storage Directory. Continuing...");

            string userData = Path.Combine(nc3Storage, "UserData");
            if (!Directory.Exists(userData))
            {
                Directory.CreateDirectory(userData);
                Console.WriteLine("UserData Directory Created");
            }
            else Console.WriteLine("Found UserData Directory. Continuing...");

            string channelData = Path.Combine(nc3Storage, "ChannelData");
            if (!Directory.Exists(channelData))
            {
                Directory.CreateDirectory(channelData);
                Console.WriteLine("ChannelData Directory Created");
            }
            else Console.WriteLine("Found ChannelData Directory. Continuing...");

            string channelContent = Path.Combine(channelData, "ChannelContent");
            if (!Directory.Exists(channelContent))
            {
                Directory.CreateDirectory(channelContent);
                Console.WriteLine("ChannelContent Directory Created");
            }
            else Console.WriteLine("Found ChannelContent Directory. Continuing...");

            string channelIcon = Path.Combine(channelData, "ChannelIcon");
            if (!Directory.Exists(channelIcon))
            {
                Directory.CreateDirectory(channelIcon);
                Console.WriteLine("ChannelIcon Directory Created");
            }
            else Console.WriteLine("Found ChannelIcon Directory. Continuing...");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Data Directory Setup Complete");
        }
        
        public static 
    }
}
