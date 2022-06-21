using System.Collections.Generic;
using System.ComponentModel;
using MySql.Data.MySqlClient;

namespace NovaAPI.Util;

public static class FriendUtils
{
    public enum FriendState
    {
        [Description("Friend")]
        Friend, 
        [Description("Pending")]
        Pending,
        [Description("Request")]
        Request,
        [Description("Accepted")]
        Accepted,
        [Description("Blocked")]
        Blocked,
        Any
    }
    public static Dictionary<string, string> GetFriends(string user_uuid, FriendState state = FriendState.Any)
    {
        using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User);
        conn.Open();
        
        Dictionary<string, string> friends = new();

        if (state == FriendState.Any)
        {
            using MySqlCommand getFriends = new($"SELECT * FROM `{user_uuid}_friends`", conn);
            MySqlDataReader reader = getFriends.ExecuteReader();
            while (reader.Read())
            {
                friends.Add(reader["UUID"].ToString(), reader["State"].ToString());
            }
        }
        else
        {
            using MySqlCommand getFriends = new($"SELECT * FROM `{user_uuid}_friends` WHERE State=@state", conn);
            getFriends.Parameters.AddWithValue("@state", GlobalUtils.GetDescription(state));
            MySqlDataReader reader = getFriends.ExecuteReader();
            while (reader.Read())
            {
                friends.Add(reader["UUID"].ToString(), reader["State"].ToString());
            }
        }

        return friends;
    }
}