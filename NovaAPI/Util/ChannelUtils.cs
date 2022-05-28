using MySql.Data.MySqlClient;
using NovaAPI.Controllers;

namespace NovaAPI.Util
{
    public static class ChannelUtils
    {
        public static bool CheckUserChannelAccess(NovaChatDatabaseContext Context, string user_uuid, string channel_uuid)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand cmd = new($"SELECT * FROM `access_{channel_uuid}` WHERE (User_UUID=@uuid)", conn);
            cmd.Parameters.AddWithValue("@uuid", user_uuid);
            MySqlDataReader reader = cmd.ExecuteReader();
            while(reader.Read())
            {
                if (!(bool)reader["Deleted"]) return true;
            }
            return false;
        }

        public static bool ChannelExsists(NovaChatDatabaseContext Context, string channel_uuid)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand cmd = new($"SELECT * FROM Channels WHERE (Table_ID=@table)", conn);
            cmd.Parameters.AddWithValue("@table", channel_uuid);
            MySqlDataReader reader = cmd.ExecuteReader();
            return reader.HasRows;
        }
    }
}
