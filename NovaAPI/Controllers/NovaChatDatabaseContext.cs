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
            MainDatabase = config.GetConnectionString("MainDatabase");
            ChannelsDatabase = config.GetConnectionString("ChannelMessages");
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
    }
}
