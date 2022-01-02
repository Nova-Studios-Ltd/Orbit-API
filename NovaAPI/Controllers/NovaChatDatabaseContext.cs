using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Controllers
{
    public class NovaChatDatabaseContext
    {
        public string MainDatabase { get; set; }
        public string ChannelsDatabase { get; set; }

        public NovaChatDatabaseContext(IConfigurationRoot config)
        {
#if !DEBUG
            MainDatabase = config.GetConnectionString("MainDatabase");
            ChannelsDatabase = config.GetConnectionString("ChannelMessages");
#endif
#if DEBUG
            MainDatabase = "server=10.0.0.250;port=3306;database=NovaChatUsers;user=nova;password=17201311;";
            ChannelsDatabase = "server=10.0.0.250;port=3306;database=NovaChatChannels;user=nova;password=17201311;";
#endif
        }

        public MySqlConnection GetUsers()
        {
            return new MySqlConnection(MainDatabase);
        }

        public MySqlConnection GetChannels()
        {
            return new MySqlConnection(ChannelsDatabase);
        }

        public string GetUserUUID(string token)
        {
            using (MySqlConnection conn = GetUsers())
            {
                conn.Open();
                MySqlCommand cmd = new("SELECT UUID FROM Users WHERE (Token=@token)", conn);
                cmd.Parameters.AddWithValue("@token", token);
                using MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    return reader["UUID"].ToString();
                }
            }
            return "";
        }
        public string GetUserUsername(string user_uuid)
        {
            using (MySqlConnection conn = GetUsers())
            {
                conn.Open();
                MySqlCommand cmd = new("SELECT Username FROM Users WHERE (UUID=@uuid)", conn);
                cmd.Parameters.AddWithValue("@uuid", user_uuid);
                using MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    return reader["Username"].ToString();
                }
            }
            return "";
        }
        public bool UserExsists(string user_uuid)
        {
            using (MySqlConnection conn = GetUsers())
            {
                conn.Open();
                MySqlCommand cmd = new("SELECT UUID FROM Users WHERE (UUID=@uuid)", conn);
                cmd.Parameters.AddWithValue("@uuid", user_uuid);
                using MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    return true;
                }
            }
            return false;
        }
        public string GetUserPubKey(string user_uuid)
        {
            using MySqlConnection conn = GetUsers();
            conn.Open();
            using MySqlCommand cmd = new($"SELECT PubKey FROM User WHERE UUID=@uuid", conn);
            cmd.Parameters.AddWithValue("@uuid", user_uuid);
            MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                return reader["PubKey"].ToString();
            }
            return "";
        }

        public void SetUserPubKey(string user_uuid, string key_user_uuid, string key)
        {
            using MySqlConnection conn = GetUsers();
            conn.Open();
            using MySqlCommand cmd = new($"INSERT INTO `{user_uuid}_keystore` (UUID, Key) VALUES (@uuid, @key)", conn);
            cmd.Parameters.AddWithValue("@uuid", key_user_uuid);
            cmd.Parameters.AddWithValue("@key", key);
            cmd.ExecuteNonQuery();
        }
    }
}
