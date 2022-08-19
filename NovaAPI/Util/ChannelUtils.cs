using System.Collections.Generic;
using MySql.Data.MySqlClient;
using NovaAPI.Controllers;

namespace NovaAPI.Util
{
    public static class ChannelUtils
    {
        public static bool CheckUserChannelAccess(string user_uuid, string channel_uuid, bool includeDeleted = false)
        {
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Channel);
            conn.Open();
            using MySqlCommand cmd = new($"SELECT * FROM `access_{channel_uuid}` WHERE (User_UUID=@uuid)", conn);
            cmd.Parameters.AddWithValue("@uuid", user_uuid);
            MySqlDataReader reader = cmd.ExecuteReader();
            while(reader.Read())
            {
                if (includeDeleted) return true;
                if (!(bool)reader["Deleted"]) return true;
            }
            return false;
        }

        public static bool ChannelExsists(string channel_uuid)
        {
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Master);
            conn.Open();
            using MySqlCommand cmd = new($"SELECT * FROM Channels WHERE (Table_ID=@table)", conn);
            cmd.Parameters.AddWithValue("@table", channel_uuid);
            MySqlDataReader reader = cmd.ExecuteReader();
            return reader.HasRows;
        }

        public static string[] GetRecipents(string channel_uuid, string user_uuid, bool includedDeleted = false)
        {
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Channel);
            conn.Open();
            using MySqlCommand cmd = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = cmd.ExecuteReader();
            List<string> recips = new List<string>();
            while (reader.Read())
            {
                string uuid = reader["User_UUID"].ToString();
                if (uuid == user_uuid) continue;
                recips.Add(uuid);
            }

            return recips.ToArray();
        }

        public static bool IsGroup(string channel_uuid)
        {
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Master);
            conn.Open();
            using MySqlCommand cmd = new($"SELECT IsGroup FROM Channels WHERE (Table_ID=@table)", conn);
            cmd.Parameters.AddWithValue("@table", channel_uuid);
            MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if ((bool) reader["IsGroup"]) return true;
            }

            return false;
        }
    }
}
