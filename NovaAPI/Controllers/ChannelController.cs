using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using MySql.Data.MySqlClient;
using NovaAPI.Attri;
using NovaAPI.Models;
using NovaAPI.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace NovaAPI.Controllers
{
    [Route("Channel")]
    [ApiController]
    [TokenAuthorization]
    public class ChannelController : ControllerBase
    {
        private readonly NovaChatDatabaseContext Context;
        private readonly EventManager Event;
        public ChannelController(NovaChatDatabaseContext context, EventManager em) 
        {
            Context = context;
            Event = em;
        }

        [HttpPost("CreateChannel")]
        public ActionResult<string> CreateChannel(string recipient_uuid) 
        {
            string author = Context.GetUserUUID(this.GetToken());
            if (string.IsNullOrEmpty(recipient_uuid) || !Context.UserExsists(recipient_uuid)) return StatusCode(500);
            if (author == recipient_uuid) return StatusCode(500);
            if (UsersShareChannel(author, recipient_uuid)) return StatusCode(403, "Channel Already Created");

            string table_id = Guid.NewGuid().ToString("N");
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();

                // Create table to hold sent messages
                using MySqlCommand createTable = new($"CREATE TABLE `{table_id}` (Message_ID BIGINT NOT NULL AUTO_INCREMENT, Author_UUID CHAR(255) NOT NULL , Content VARCHAR(4000) NOT NULL , CreationDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP , PRIMARY KEY (Message_ID)) ENGINE = InnoDB;", conn);
                createTable.ExecuteNonQuery();

                // Create table to hold the users attached to this channel
                using MySqlCommand createAccessTable = new($"CREATE TABLE `access_{table_id}` (User_UUID CHAR(255) NOT NULL , UNIQUE User_UUIDs (User_UUID)) ENGINE = InnoDB;", conn);
                createAccessTable.ExecuteNonQuery();

                // Add users to access table
                using MySqlCommand addUserAccess = new($"INSERT INTO  `access_{table_id}` (User_UUID) VALUES (@author), (@recipient)", conn);
                addUserAccess.Parameters.AddWithValue("@author", author);
                addUserAccess.Parameters.AddWithValue("@recipient", recipient_uuid);
                addUserAccess.ExecuteNonQuery();

                // Add table id to channels table
                using MySqlCommand addChannel = new($"INSERT INTO `Channels` (`Table_ID`, `Owner_UUID`, `ChannelIcon`, `Timestamp`) VALUES (@table_id, @owner_uuid, @icon, CURRENT_TIMESTAMP)", conn);
                addChannel.Parameters.AddWithValue("@table_id", table_id);
                addChannel.Parameters.AddWithValue("@owner_uuid", Context.GetUserUUID(this.GetToken()));
                addChannel.Parameters.AddWithValue("@icon", Path.GetFileName(MediaController.DefaultAvatars[MediaController.GetRandom.Next(0, MediaController.DefaultAvatars.Length - 1)]));
                addChannel.ExecuteNonQuery();
            }

            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                try
                {
                    // Add channel to receiver
                    MySqlCommand cmd = new($"INSERT INTO `{recipient_uuid}` (Property, Value) VALUES (@property, @uuid)", conn);
                    cmd.Parameters.AddWithValue("@property", "ChannelAccess");
                    cmd.Parameters.AddWithValue("@uuid", table_id);
                    cmd.ExecuteNonQuery();

                    // Add channel to author
                    cmd = new($"INSERT INTO `{Context.GetUserUUID(this.GetToken())}` (Property, Value) VALUES (@property, @uuid)", conn);
                    cmd.Parameters.AddWithValue("@property", "ChannelAccess");
                    cmd.Parameters.AddWithValue("@uuid", table_id);
                    cmd.ExecuteNonQuery();

                    cmd.Dispose();
                }
                catch
                {
                    return StatusCode(500);
                }
            }
            Event.ChannelCreatedEvent(table_id);
            return table_id;
        }

        // Groups
        [HttpPost("CreateGroupChannel")]
        public ActionResult<string> CreateGroupChannel(string group_name, List<string> recipients) 
        {
            string author = Context.GetUserUUID(this.GetToken());
            if (recipients.Any(x => x == author)) return StatusCode(500);

            string table_id = Guid.NewGuid().ToString("N");
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();

                // Create table to hold sent messages
                using MySqlCommand createTable = new($"CREATE TABLE `{table_id}` (Message_ID BIGINT NOT NULL AUTO_INCREMENT, Author_UUID CHAR(255) NOT NULL , Content VARCHAR(4000) NOT NULL , CreationDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP , PRIMARY KEY (Message_ID)) ENGINE = InnoDB;", conn);
                createTable.ExecuteNonQuery();

                // Create table to hold the users attached to this channel
                using MySqlCommand createAccessTable = new($"CREATE TABLE `access_{table_id}` (User_UUID CHAR(255) NOT NULL , UNIQUE User_UUIDs (User_UUID)) ENGINE = InnoDB;", conn);
                createAccessTable.ExecuteNonQuery();

                // Add users to access table
                foreach (string recipient in recipients)
                {
                    if (string.IsNullOrEmpty(recipient) || !Context.UserExsists(recipient)) continue;
                    using MySqlCommand addUserAccess = new($"INSERT INTO  `access_{table_id}` (User_UUID) VALUES (@recipient)", conn);
                    addUserAccess.Parameters.AddWithValue("@recipient", recipient);
                    addUserAccess.ExecuteNonQuery();
                }

                using MySqlCommand addAuthor = new($"INSERT INTO  `access_{table_id}` (User_UUID) VALUES (@recipient)", conn);
                addAuthor.Parameters.AddWithValue("@recipient", Context.GetUserUUID(this.GetToken()));
                addAuthor.ExecuteNonQuery();

                // Add table id to channels table
                using MySqlCommand addChannel = new($"INSERT INTO `Channels` (`Table_ID`, `Owner_UUID`, `ChannelIcon`, `IsGroup`, `Timestamp`, `GroupName`) VALUES (@table_id, @owner_uuid, @icon, @group, CURRENT_TIMESTAMP, @gn)", conn);
                addChannel.Parameters.AddWithValue("@table_id", table_id);
                addChannel.Parameters.AddWithValue("@owner_uuid", Context.GetUserUUID(this.GetToken()));
                addChannel.Parameters.AddWithValue("@icon", Path.GetFileName(MediaController.DefaultAvatars[MediaController.GetRandom.Next(0, MediaController.DefaultAvatars.Length - 1)]));
                addChannel.Parameters.AddWithValue("@group", true);
                addChannel.Parameters.AddWithValue("@gn", group_name);
                addChannel.ExecuteNonQuery();
            }

            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                try
                {

                    foreach (string recipient in recipients)
                    {
                        if (string.IsNullOrEmpty(recipient) || !Context.UserExsists(recipient)) continue;
                        // Add channel to receiver
                        MySqlCommand addToRec = new($"INSERT INTO `{recipient}` (Property, Value) VALUES (@property, @uuid)", conn);
                        addToRec.Parameters.AddWithValue("@property", "ChannelAccess");
                        addToRec.Parameters.AddWithValue("@uuid", table_id);
                        addToRec.ExecuteNonQuery();
                    }

                    // Add channel to author
                    MySqlCommand cmd = new($"INSERT INTO `{Context.GetUserUUID(this.GetToken())}` (Property, Value) VALUES (@property, @uuid)", conn);
                    cmd.Parameters.AddWithValue("@property", "ChannelAccess");
                    cmd.Parameters.AddWithValue("@uuid", table_id);
                    cmd.ExecuteNonQuery();

                    cmd.Dispose();
                }
                catch
                {
                    return StatusCode(500);
                }
            }
            Event.ChannelCreatedEvent(table_id);
            return table_id;
        }

        [HttpPatch("{channel_uuid}/Members")]
        public ActionResult AddUserToGroupChannel(string channel_uuid, List<string> recipients) 
        {
            if (!CheckUserChannelAccess(Context.GetUserUUID(this.GetToken()), channel_uuid)) return StatusCode(403);
            Channel c = GetChannel(channel_uuid).Value;
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                try
                {
                    // Add channel to user
                    foreach (string recipient in recipients)
                    {
                        if (string.IsNullOrEmpty(recipient) || !Context.UserExsists(recipient)) continue;
                        using MySqlCommand cmd = new($"INSERT INTO `{recipient}` (Property, Value) VALUES (@property, @uuid)", conn);
                        cmd.Parameters.AddWithValue("@property", "ChannelAccess");
                        cmd.Parameters.AddWithValue("@uuid", channel_uuid);
                        cmd.ExecuteNonQuery();
                        cmd.Dispose();
                    }
                }
                catch
                {
                    return StatusCode(500);
                }
            }

            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                if (!c.IsGroup && c.Members.Count > 2) {
                    using MySqlCommand updateType = new($"UPDATE Channels SET IsGroup=@isGroup WHERE (Table_ID=@channel_uuid)", conn);
                    updateType.Parameters.AddWithValue("@isGroup", true);
                    updateType.Parameters.AddWithValue("@channel_uuid", channel_uuid);
                    updateType.ExecuteNonQuery();
                }

                // Add user to channel
                foreach (string recipient in recipients)
                {
                    if (string.IsNullOrEmpty(recipient) || !Context.UserExsists(recipient)) continue;
                    Event.UserNewGroup(channel_uuid, recipient);
                    using MySqlCommand cmd = new($"INSERT INTO `access_{channel_uuid}` (User_UUID) VALUES (@uuid)", conn);
                    cmd.Parameters.AddWithValue("@uuid", recipient);
                    cmd.ExecuteNonQuery();
                }
            }
            return StatusCode(200);
        }

        [HttpDelete("{channel_uuid}/Members")]
        public ActionResult RemoveUserFromGroupChannel(string channel_uuid, string recipient) 
        {
            if (!CheckUserChannelOwner(channel_uuid, recipient)) return StatusCode(403);
            if (string.IsNullOrEmpty(recipient) || !Context.UserExsists(recipient)) return StatusCode(500);
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                try
                {
                    // Add channel to user
                    using MySqlCommand cmd = new($"REMOVE FROM `{Context.GetUserUUID(this.GetToken())}` WHERE (Property=@property) AND (Value=@uuid)", conn);
                    cmd.Parameters.AddWithValue("@property", "ChannelAccess");
                    cmd.Parameters.AddWithValue("@uuid", channel_uuid);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                }
                catch
                {
                    return StatusCode(500);
                }
            }

            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                // Add user to channel
                using MySqlCommand cmd = new($"DELETE FROM `access_{channel_uuid}` WHERE (User_UUID=@uuid)", conn);
                cmd.Parameters.AddWithValue("@uuid", recipient);
                cmd.ExecuteNonQuery();
            }
            return StatusCode(200);
        }

        [HttpPatch("{channel_uuid}/Name")]
        public ActionResult UpdateGroupName(string channel_uuid, string new_name) 
        {
            if (string.IsNullOrEmpty(new_name) && string.IsNullOrWhiteSpace(new_name)) return StatusCode(400, "Name cannot be empty");
            using (MySqlConnection conn = Context.GetChannels()) {
                conn.Open();
                using MySqlCommand updateType = new($"UPDATE Channels SET GroupName=@name WHERE (Table_ID=@channel_uuid)", conn);
                updateType.Parameters.AddWithValue("@name", new_name);
                updateType.Parameters.AddWithValue("@channel_uuid", channel_uuid);
                updateType.ExecuteNonQuery();
            }
            return StatusCode(200);
        }

        // General Channel
        [HttpGet("{channel_uuid}")]
        public ActionResult<Channel> GetChannel(string channel_uuid) 
        {
            string user_uuid = Context.GetUserUUID(this.GetToken());
            if (!CheckUserChannelAccess(user_uuid, channel_uuid)) return StatusCode(403);
            Channel channel = new();
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                MySqlCommand retreiveChannel = new($"SELECT * FROM Channels WHERE (Table_ID=@table_id)", conn);
                retreiveChannel.Parameters.AddWithValue("@table_id", channel_uuid);
                MySqlDataReader reader = retreiveChannel.ExecuteReader();
                while (reader.Read())
                {
                    channel.Table_Id = channel_uuid;
                    channel.Owner_UUID = (string)reader["Owner_UUID"];
                    channel.IsGroup = (bool)reader["IsGroup"];
                    channel.GroupName = (string)reader["GroupName"];
                    channel.ChannelIcon = $"https://api.novastudios.tk/Media/Channel/{channel_uuid}?size=64";
                        
                }
                reader.Close();
                MySqlCommand retreiveMembers = new($"SELECT User_UUID FROM `access_{channel_uuid}`", conn);
                reader = retreiveMembers.ExecuteReader();
                channel.Members = new();
                while (reader.Read())
                {
                    channel.Members.Add((string)reader["User_UUID"]);
                }

                if (!channel.IsGroup)
                {
                    foreach (string member in channel.Members)
                    {
                        if (member == user_uuid) continue;
                        channel.ChannelName = Context.GetUserUsername(member);
                        channel.ChannelIcon = $"https://api.novastudios.tk/Media/Avatar/{member}?size=64";
                    }
                }
            }
            return channel;
        }

        [HttpDelete("{channel_uuid}")]
        public ActionResult DeleteChannel(string channel_uuid) 
        {
            using (MySqlConnection conn = Context.GetChannels())
            {
                Channel c = GetChannel(channel_uuid).Value;
                if (c == null) return StatusCode(404);
                conn.Open();

                if (c.IsGroup)
                {
                    // Remove channel from Channels table
                    using MySqlCommand cmd = new($"DELETE FROM Channels WHERE (Table_ID=@table_id) AND (Owner_UUID=@owner_uuid)", conn);
                    cmd.Parameters.AddWithValue("@table_id", channel_uuid);
                    cmd.Parameters.AddWithValue("@owner_uuid", Context.GetUserUUID(this.GetToken()));
                    if (cmd.ExecuteNonQuery() == 0) return NotFound();

                    // Remove channel from user
                    using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
                    MySqlDataReader reader = removeUsers.ExecuteReader();
                    using MySqlConnection userDb = Context.GetUsers();
                    userDb.Open();
                    while (reader.Read())
                    {
                        using MySqlCommand removeAccess = new($"DELETE FROM `{reader["User_UUID"]}` WHERE (Property=@prop) AND (Value=@value)", userDb);
                        removeAccess.Parameters.AddWithValue("@prop", "ChannelAccess");
                        removeAccess.Parameters.AddWithValue("@value", channel_uuid);
                        removeAccess.ExecuteNonQuery();
                    }

                    conn.Close();
                    conn.Open();

                    // Remove channel table (removing messages)
                    using MySqlCommand deleteChannel = new($"DROP TABLE `{channel_uuid}`", conn);
                    deleteChannel.ExecuteNonQuery();

                    // Remove channel access table
                    using MySqlCommand deleteAccessTable = new($"DROP TABLE `access_{channel_uuid}`", conn);
                    deleteAccessTable.ExecuteNonQuery();
                }
                else
                {
                    if (c.Members.Count <= 1)
                    {
                        // Remove channel from Channels table
                        using MySqlCommand cmd = new($"DELETE FROM Channels WHERE (Table_ID=@table_id)", conn);
                        cmd.Parameters.AddWithValue("@table_id", channel_uuid);
                        if (cmd.ExecuteNonQuery() == 0) return NotFound();

                        // Remove channel from user
                        using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
                        MySqlDataReader reader = removeUsers.ExecuteReader();
                        using MySqlConnection userDb = Context.GetUsers();
                        userDb.Open();
                        while (reader.Read())
                        {
                            using MySqlCommand removeAccess = new($"DELETE FROM `{reader["User_UUID"]}` WHERE (Property=@prop) AND (Value=@value)", userDb);
                            removeAccess.Parameters.AddWithValue("@prop", "ChannelAccess");
                            removeAccess.Parameters.AddWithValue("@value", channel_uuid);
                            removeAccess.ExecuteNonQuery();
                        }

                        conn.Close();
                        conn.Open();

                        // Remove channel table (removing messages)
                        using MySqlCommand deleteChannel = new($"DROP TABLE `{channel_uuid}`", conn);
                        deleteChannel.ExecuteNonQuery();

                        // Remove channel access table
                        using MySqlCommand deleteAccessTable = new($"DROP TABLE `access_{channel_uuid}`", conn);
                        deleteAccessTable.ExecuteNonQuery();
                    }
                    else
                    {
                        using MySqlCommand removeUser =
                            new MySqlCommand($"DELETE FROM `access_{channel_uuid}` WHERE User_UUID=@uuid", conn);
                        removeUser.Parameters.AddWithValue("@uuid", Context.GetUserUUID(this.GetToken()));
                        removeUser.ExecuteNonQuery();
                    }
                }
            }
            return StatusCode(200, "Channel has been removed");
        }

        bool CheckUserChannelAccess(string userUUID, string channel_uuid) 
        {
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();
            using MySqlCommand cmd = new($"SELECT Property FROM {userUUID} WHERE (Property=@prop) AND (Value=@channel_uuid)", conn);
            cmd.Parameters.AddWithValue("@prop", "ChannelAccess");
            cmd.Parameters.AddWithValue("@channel_uuid", channel_uuid);
            MySqlDataReader reader = cmd.ExecuteReader();
            if (reader.HasRows) return true;
            return false;
        }

        bool CheckUserChannelOwner(string channel_uuid, string user_uuid) 
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand cmd = new($"SELECT Owner_UUID FROM Channels WHERE (Table_ID=@channel_uuid)", conn);
            cmd.Parameters.AddWithValue("@channel_uuid", channel_uuid);
            MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if ((string)reader["Owner_UUID"] == user_uuid) return true;
            }
            return false;
        }

        private string GetAvatarFile(string user_uuid) 
        {
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                MySqlCommand cmd = new($"SELECT Avatar FROM Users WHERE (UUID=@uuid)", conn);
                cmd.Parameters.AddWithValue("@uuid", user_uuid);
                using MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    return (string)reader["Avatar"];
                }
            }
            return "";
        }

        private bool UsersShareChannel(string user_uuid1, string user_uuid2) {
            List<string> user1_channels = GetUserChannels(user_uuid1);
            List<string> user2_channels = GetUserChannels(user_uuid2);
            
            string[] matchingChannels = user1_channels.Intersect(user2_channels).ToArray();

            foreach (string channel in matchingChannels)
            {
                Channel c = GetChannel(channel).Value;
                if (!c.IsGroup)
                {
                    if (c.Members.Contains(user_uuid1) && c.Members.Contains(user_uuid2))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private List<string> GetUserChannels(string user_uuid) {
            List<string> channels = new();
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                using MySqlCommand cmd = new($"SELECT * FROM `{user_uuid}` WHERE (Property=@prop)", conn);
                cmd.Parameters.AddWithValue("@prop", "ChannelAccess");
                MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    channels.Add((string)reader["Value"]);
                }
            }
            return channels;
        }
    }
}
