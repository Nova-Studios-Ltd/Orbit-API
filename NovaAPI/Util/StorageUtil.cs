using Microsoft.Extensions.Configuration;
using NovaAPI.Controllers;
using NovaAPI.DataTypes;
using System;
using System.IO;
using MimeTypes;
using MySql.Data.MySqlClient;
using NovaAPI.Interfaces;
using System.Reflection;

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
            #if DEBUG
            return;
            #endif
            
            Context = new NovaChatDatabaseContext(config);
            
            if (directory == "") directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (directory == null) throw new ArgumentException("directory is null");

            NC3Storage = Path.Combine(directory, "NC3Storage");
            Console.ForegroundColor = ConsoleColor.Green;
            if (!Directory.Exists(NC3Storage))
            {
                Directory.CreateDirectory(NC3Storage);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Storage ({NC3Storage}) Directory Created");
            }
            else Console.WriteLine($"Found Storage ({NC3Storage}) Directory. Continuing...");

            Console.ForegroundColor = ConsoleColor.Green;

            UserData = Path.Combine(NC3Storage, "UserData");
            if (!Directory.Exists(UserData))
            {
                Directory.CreateDirectory(UserData);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"UserData ({UserData}) Directory Created");
            }
            else Console.WriteLine($"Found UserData ({UserData}) Directory. Continuing...");

            Console.ForegroundColor = ConsoleColor.Green;

            ChannelData = Path.Combine(NC3Storage, "ChannelData");
            if (!Directory.Exists(ChannelData))
            {
                Directory.CreateDirectory(ChannelData);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"ChannelData ({ChannelData}) Directory Created");
            }
            else Console.WriteLine($"Found ChannelData ({ChannelData}) Directory. Continuing...");

            Console.ForegroundColor = ConsoleColor.Green;

            ChannelContent = Path.Combine(ChannelData, "ChannelContent");
            if (!Directory.Exists(ChannelContent))
            {
                Directory.CreateDirectory(ChannelContent);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"ChannelContent ({ChannelContent}) Directory Created");
            }
            else Console.WriteLine($"Found ChannelContent ({ChannelContent}) Directory. Continuing...");

            Console.ForegroundColor = ConsoleColor.Green;

            ChannelIcon = Path.Combine(ChannelData, "ChannelIcon");
            if (!Directory.Exists(ChannelIcon))
            {
                Directory.CreateDirectory(ChannelIcon);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"ChannelIcon ({ChannelIcon}) Directory Created");
            }
            else Console.WriteLine($"Found ChannelIcon ({ChannelIcon}) Directory. Continuing...");

            Console.WriteLine("Data Directory Setup Complete");
            Console.ForegroundColor = ConsoleColor.Gray;
        }
        
        public static string StoreFile(MediaType mediaType, Stream file, IMeta meta)
        {
            if (mediaType == MediaType.ChannelContent)
            {
                ChannelContentMeta filemeta = (ChannelContentMeta) meta;
                string path = Path.Combine(ChannelContent, filemeta.Channel_UUID);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    Console.WriteLine($"Created Directory ({path}) For Channnel {filemeta.Channel_UUID}");
                }
                
                // Save file to disk
                string filename = GlobalUtils.CreateMD5(filemeta.Filename + DateTime.Now);
                FileStream fs = File.Create(Path.Combine(path, filename));
                file.CopyTo(fs);
                fs.Close();
                
                // Store file meta data
                using MySqlConnection conn = Context.GetChannels();
                conn.Open();
                using MySqlCommand cmd = new($"INSERT INTO ChannelMedia (File_UUID, Filename, MimeType, Size, ContentWidth, ContentHeight) VALUES (@uuid, @filename, @mime, @size, @width, @height)", conn);
                cmd.Parameters.AddWithValue("@uuid", filename);
                cmd.Parameters.AddWithValue("@filename", filemeta.Filename);
                cmd.Parameters.AddWithValue("@mime", MimeTypeMap.GetMimeType(Path.GetExtension(filemeta.Filename)));
                cmd.Parameters.AddWithValue("@size", filemeta.Filesize);
                cmd.Parameters.AddWithValue("@width", filemeta.Width);
                cmd.Parameters.AddWithValue("@height", filemeta.Height);
                if (cmd.ExecuteNonQuery() == 0) return "E";
                return filename;
            }
            else if (mediaType == MediaType.Avatar)
            {
                AvatarMeta filemeta = (AvatarMeta) meta;
                string filename = GlobalUtils.CreateMD5(filemeta.Filename + DateTime.Now);
                FileStream fs = File.Create(Path.Combine(UserData, filename));
                file.CopyTo(fs);
                fs.Close();

                using MySqlConnection conn = Context.GetUsers();
                conn.Open();
                using MySqlCommand setAvatar = new($"UPDATE Users SET Avatar=@avatar WHERE (UUID=@uuid)", conn);
                setAvatar.Parameters.AddWithValue("@uuid", filemeta.User_UUID);
                setAvatar.Parameters.AddWithValue("@avatar", filename);
                if (setAvatar.ExecuteNonQuery() == 0) return "E";
                conn.Close();
                return "";
            }
            else
            {
                IconMeta filemeta = (IconMeta) meta;
                string filename = GlobalUtils.CreateMD5(filemeta.Filename + DateTime.Now);
                FileStream fs = File.Create(Path.Combine(ChannelIcon, filename));
                file.CopyTo(fs);
                fs.Close();

                using MySqlConnection conn = Context.GetChannels();
                conn.Open();
                using MySqlCommand setAvatar = new($"UPDATE Channels SET ChannelIcon=@avatar WHERE (Table_ID=@channel_uuid)",
                    conn);
                setAvatar.Parameters.AddWithValue("@channel_uuid", filemeta.Channel_UUID);
                setAvatar.Parameters.AddWithValue("@avatar", filename);
                if (setAvatar.ExecuteNonQuery() == 0) return "E";
                conn.Close();
                return "";
            }
        }

        public static MediaFile RetreiveFile(MediaType mediaType, string resource_id, string location_id = "")
        {
            if (mediaType == MediaType.ChannelContent)
            {
                string path = Path.Combine(ChannelContent, location_id, resource_id);
                FileStream fs = File.OpenRead(path);
                Diamension dim = RetreiveDiamension(resource_id);
                return new MediaFile(fs,
                    new ChannelContentMeta(dim.Width, dim.Height, RetreiveMimeType(resource_id), RetreiveFilename(resource_id), location_id,
                        fs.Length));
            }
            else if (mediaType == MediaType.ChannelIcon)
            {
                string path = Path.Combine(ChannelIcon, RetreiveChannelIcon(resource_id));
                if (!File.Exists(path)) return null;
                FileStream fs = File.OpenRead(path);
                return new MediaFile(fs, new IconMeta(resource_id, fs.Length, location_id));
            }
            else
            {
                string name = RetreiveUserAvatar(resource_id);
                string path = Path.Combine(UserData, name);
                FileStream fs = File.OpenRead(path);
                return new MediaFile(fs, new AvatarMeta(name, fs.Length, resource_id));
            }
        }

        public static void DeleteFile(MediaType mediaType, string resource_id, string location_id = "")
        {
            if (mediaType == MediaType.ChannelContent)
            {
                using MySqlConnection conn = Context.GetChannels();
                conn.Open();
                using MySqlCommand cmd = new("DELETE FROM `ChannelMedia` WHERE (File_UUID=@file)", conn);
                cmd.Parameters.AddWithValue("@file", resource_id);
                cmd.ExecuteNonQuery();
                conn.Close();
                string path = Path.Combine(ChannelContent, location_id, resource_id);
                Console.WriteLine(path);
                File.Delete(path);
            }
            else if (mediaType == MediaType.ChannelIcon)
            {
                string path = Path.Combine(ChannelIcon, RetreiveChannelIcon(resource_id));
                File.Delete(path);
            }
            else
            {
                string name = RetreiveUserAvatar(resource_id);
                string path = Path.Combine(UserData, name);
                File.Delete(path);
            }
        }

        public static void RemoveChannelContent(string channel_uuid)
        {
            string[] files = Directory.GetFiles(Path.Combine(ChannelContent, channel_uuid));
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            foreach (string file in files)
            {
                using MySqlCommand cmd = new("DELETE FROM `ChannelMedia` WHERE (File_UUID=@file)", conn);
                cmd.Parameters.AddWithValue("@file", new FileInfo(file).Name);
                cmd.ExecuteNonQuery();
                File.Delete(Path.Combine(ChannelContent, channel_uuid, file));
            }
            conn.Close();
            Directory.Delete(Path.Combine(ChannelContent, channel_uuid));
        }
        
        public static string RetreiveMimeType(string content_id)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand cmd = new("SELECT MimeType FROM ChannelMedia WHERE (File_UUID=@uuid)", conn);
            cmd.Parameters.AddWithValue("@uuid", content_id);
            MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                return reader["MimeType"].ToString();
            }
            return "";
        }

        public static Diamension RetreiveDiamension(string content_id)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand cmd = new("SELECT ContentWidth,ContentHeight FROM ChannelMedia WHERE (File_UUID=@uuid)", conn);
            cmd.Parameters.AddWithValue("@uuid", content_id);
            MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                return new Diamension(int.Parse(reader["ContentWidth"].ToString()), int.Parse(reader["ContentHeight"].ToString()));
            }
            return new Diamension(0, 0);
        }

        public static string RetreiveFilename(string content_id)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand cmd = new("SELECT Filename FROM ChannelMedia WHERE (File_UUID=@uuid)", conn);
            cmd.Parameters.AddWithValue("@uuid", content_id);
            MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                return reader["Filename"].ToString();
            }
            return "";
        }

        public static string RetreiveUserAvatar(string user_uuid)
        {
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();
            using MySqlCommand cmd = new($"SELECT Avatar FROM Users WHERE (UUID=@uuid)", conn);
            cmd.Parameters.AddWithValue("@uuid", user_uuid);
            using MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                return reader["Avatar"].ToString();
            }

            return "";
        }

        public static string RetreiveChannelIcon(string channel_uuid)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand cmd = new("SELECT ChannelIcon FROM Channels WHERE (Table_ID=@uuid)", conn);
            cmd.Parameters.AddWithValue("@uuid", channel_uuid);
            MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                return reader["ChannelIcon"].ToString();
            }
            return "";
        }
    }
}
