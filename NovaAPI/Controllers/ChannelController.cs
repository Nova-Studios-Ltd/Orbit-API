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
        // "The codebase must grow" - Andy 2021
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
                using MySqlCommand createTable = new($"CREATE TABLE `{table_id}` (Message_ID BIGINT NOT NULL AUTO_INCREMENT, Author_UUID CHAR(255) NOT NULL, Content VARCHAR(4000) NOT NULL, Attachments JSON NOT NULL, CreationDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP , PRIMARY KEY (Message_ID)) ENGINE = InnoDB;", conn);
                createTable.ExecuteNonQuery();

                // Create table to hold the users attached to this channel
                using MySqlCommand createAccessTable = new($"CREATE TABLE `access_{table_id}` (User_UUID CHAR(255) NOT NULL, DELETED BOOLEAN NOT NULL DEFAULT FALSE, UNIQUE User_UUIDs (User_UUID)) ENGINE = InnoDB;", conn);
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
                    cmd.Parameters.AddWithValue("@property", "ActiveChannelAccess");
                    cmd.Parameters.AddWithValue("@uuid", table_id);
                    cmd.ExecuteNonQuery();

                    // Add channel to author
                    cmd = new($"INSERT INTO `{Context.GetUserUUID(this.GetToken())}` (Property, Value) VALUES (@property, @uuid)", conn);
                    cmd.Parameters.AddWithValue("@property", "ActiveChannelAccess");
                    cmd.Parameters.AddWithValue("@uuid", table_id);
                    cmd.ExecuteNonQuery();

                    cmd.Dispose();
                }
                catch
                {
                    return StatusCode(500);
                }
            }

            Directory.CreateDirectory(Path.Combine(GlobalUtils.ChannelMedia, table_id));
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
                using MySqlCommand createTable = new($"CREATE TABLE `{table_id}` (Message_ID BIGINT NOT NULL AUTO_INCREMENT, Author_UUID CHAR(255) NOT NULL , Content VARCHAR(4000) NOT NULL , Attachments JSON NOT NULL, CreationDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP , PRIMARY KEY (Message_ID)) ENGINE = InnoDB;", conn);
                createTable.ExecuteNonQuery();

                // Create table to hold the users attached to this channel
                using MySqlCommand createAccessTable = new($"CREATE TABLE `access_{table_id}` (User_UUID CHAR(255) NOT NULL, DELETED BOOLEAN NOT NULL DEFAULT FALSE, UNIQUE User_UUIDs (User_UUID)) ENGINE = InnoDB;", conn);
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
                        addToRec.Parameters.AddWithValue("@property", "ActiveChannelAccess");
                        addToRec.Parameters.AddWithValue("@uuid", table_id);
                        addToRec.ExecuteNonQuery();
                    }

                    // Add channel to author
                    MySqlCommand cmd = new($"INSERT INTO `{Context.GetUserUUID(this.GetToken())}` (Property, Value) VALUES (@property, @uuid)", conn);
                    cmd.Parameters.AddWithValue("@property", "ActiveChannelAccess");
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
            if (!ChannelUtils.CheckUserChannelAccess(Context, Context.GetUserUUID(this.GetToken()), channel_uuid)) return StatusCode(403);
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
                        cmd.Parameters.AddWithValue("@property", "ActiveChannelAccess");
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
            string user_uuid = Context.GetUserUUID(this.GetToken());
            if (!CheckUserChannelOwner(channel_uuid, user_uuid) || recipient != user_uuid) return StatusCode(403);
            if (string.IsNullOrEmpty(recipient) || !Context.UserExsists(recipient)) return StatusCode(500);
            Channel c = GetChannel(channel_uuid).Value;
            if (c == null) return StatusCode(400);
            if (c.IsGroup) return StatusCode(405);
            
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                try
                {
                    // Delete channel from user
                    using MySqlCommand cmd = new($"DELETE FROM `{recipient}` WHERE (Property=@property) AND (Value=@uuid)", conn);
                    cmd.Parameters.AddWithValue("@property", "ActiveChannelAccess");
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
            Event.ChannelDeleteEvent(channel_uuid, user_uuid);
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
            if (!ChannelUtils.CheckUserChannelAccess(Context, user_uuid, channel_uuid)) return StatusCode(403);
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
                    channel.ChannelName = (string)reader["GroupName"];
                    channel.ChannelIcon = $"https://api.novastudios.tk/Media/Channel/{channel_uuid}/Icon?size=64";
                        
                }
                reader.Close();
                MySqlCommand retreiveMembers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
                reader = retreiveMembers.ExecuteReader();
                channel.Members = new();
                while (reader.Read())
                {
                    string member = (string)reader["User_UUID"];
                    if (!(bool)reader["DELETED"])
                        channel.Members.Add(member);
                    if (!channel.IsGroup)
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
        public ActionResult RemoveChannel(string channel_uuid)
        {
            string user_uuid = Context.GetUserUUID(this.GetToken());
            if (!ChannelUtils.CheckUserChannelAccess(Context, user_uuid, channel_uuid)) return StatusCode(403, "Permission Denied");

            Channel channel = GetChannel(channel_uuid).Value;

            // Handle Standard Channel
            if (!channel.IsGroup)
            {
                // Remove Channel from User props
                using MySqlConnection userConn = Context.GetUsers();
                userConn.Open();
                using MySqlCommand removeFromUser = new($"DELETE FROM `{user_uuid}` WHERE (Property=@prop1) OR (Property=@prop2) AND (Value=@channel)", userConn);
                removeFromUser.Parameters.AddWithValue("@prop1", "ArchivedChannelAccess");
                removeFromUser.Parameters.AddWithValue("@prop2", "ActiveChannelAccess");
                removeFromUser.Parameters.AddWithValue("@channel", channel_uuid);
                if (removeFromUser.ExecuteNonQuery() == 0) return StatusCode(404);
                userConn.Close();

                if (channel.Members.Count <= 1)
                {
                    // Remove Access Table and Chat History
                    using MySqlConnection channelCon = Context.GetChannels();
                    channelCon.Open();
                    using MySqlCommand removeChannel = new($"DROP TABLE `{channel_uuid}`, `access_{channel_uuid}`", channelCon);
                    removeChannel.ExecuteNonQuery();
                  
                    // Remove Channel
                    using MySqlCommand cmd = new($"DELETE FROM Channels WHERE (Table_ID=@table_id)", channelCon);
                    cmd.Parameters.AddWithValue("@table_id", channel_uuid);
                    if (cmd.ExecuteNonQuery() == 0) return NotFound();
                    channelCon.Close();
                    Event.ChannelDeleteEvent(channel_uuid, user_uuid);

                    Directory.Delete(Path.Combine(GlobalUtils.ChannelMedia, channel_uuid), true);

                    return StatusCode(200, "Channel Removed");
                }
                else
                {
                    // Set user to deleted in access table
                    using MySqlConnection channelCon = Context.GetChannels();
                    channelCon.Open();
                    using MySqlCommand updateAccess = new($"UPDATE `access_{channel_uuid}` SET DELETED=1 WHERE (User_UUID=@uuid)", channelCon);
                    updateAccess.Parameters.AddWithValue("@uuid", user_uuid);
                    if (updateAccess.ExecuteNonQuery() == 0) return StatusCode(404);
                    channelCon.Close();
                    Event.ChannelDeleteEvent(channel_uuid, user_uuid);
                    return StatusCode(200, "Channel Removed");
                }
            }
            else
            {
                if (CheckUserChannelOwner(channel_uuid, user_uuid))
                {
                    // Delete channel from Channels table
                    using MySqlConnection channelCon = Context.GetChannels();
                    channelCon.Open();
                    using MySqlCommand removeChannel = new($"DELETE FROM Channels WHERE (Table_ID=@table_id) AND (Owner_UUID=@owner_uuid)", channelCon);
                    removeChannel.Parameters.AddWithValue("@table_id", channel_uuid);
                    removeChannel.Parameters.AddWithValue("@owner_uuid", user_uuid);
                    if (removeChannel.ExecuteNonQuery() == 0) return StatusCode(404);

                    // Get all users with access
                    using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", channelCon);
                    MySqlDataReader reader = removeUsers.ExecuteReader();
                    using MySqlConnection userDb = Context.GetUsers();
                    userDb.Open();
                    while (reader.Read())
                    {   
                        // Delete channel from User props table
                        using MySqlCommand removeAccess = new($"DELETE FROM `{reader["User_UUID"]}` WHERE (Property=@prop) AND (Value=@value)", userDb);
                        removeAccess.Parameters.AddWithValue("@prop", "ActiveChannelAccess");
                        removeAccess.Parameters.AddWithValue("@value", channel_uuid);
                        removeAccess.ExecuteNonQuery();
                    }
                    userDb.Close();

                    using MySqlCommand removeChannelTables = new($"DROP TABLE `{channel_uuid}`, `access_{channel_uuid}`", channelCon);
                    removeChannelTables.ExecuteNonQuery();
                    channelCon.Close();
                    
                    Event.ChannelDeleteEvent(channel_uuid);

                    Directory.Delete(Path.Combine(GlobalUtils.ChannelMedia, channel_uuid), true);

                    return StatusCode(200, "Group Removed");
                }
                else
                {
                    // Delete channel from User props table
                    using MySqlConnection userDb = Context.GetUsers();
                    userDb.Open();
                    using MySqlCommand removeAccess = new($"DELETE FROM `{user_uuid}` WHERE (Property=@prop) AND (Value=@value)", userDb);
                    removeAccess.Parameters.AddWithValue("@prop", "ActiveChannelAccess");
                    removeAccess.Parameters.AddWithValue("@value", channel_uuid);
                    removeAccess.ExecuteNonQuery();
                    userDb.Close();

                    // Delete user from Channel Access table
                    using MySqlConnection channelsDB = Context.GetChannels();
                    channelsDB.Open();
                    using MySqlCommand cmd = new($"DELETE FROM `access_{channel_uuid}` WHERE (User_UUID=@uuid)", channelsDB);
                    cmd.Parameters.AddWithValue("@uuid", user_uuid);
                    cmd.ExecuteNonQuery();
                    return StatusCode(200, "Group Removed");
                }
            }
        }

        [HttpPatch("{channel_uuid}/Archive")]
        public ActionResult ArchiveChannel(string channel_uuid)
        {
            string user_uuid = Context.GetUserUUID(this.GetToken());
            if (!ChannelUtils.CheckUserChannelAccess(Context, user_uuid, channel_uuid)) return StatusCode(403, "Permission Denied");
            if (GetChannel(channel_uuid).Value.IsGroup) return StatusCode(405, "Can not Archive group");
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();
            using MySqlCommand cmd = new($"UPDATE `{user_uuid}` SET Property=@prop WHERE (Value=@channel)", conn);
            cmd.Parameters.AddWithValue("@prop", "ArchivedChannelAccess");
            cmd.Parameters.AddWithValue("@channel", channel_uuid);
            if (cmd.ExecuteNonQuery() > 0) return StatusCode(200);
            return StatusCode(404);
        }

        [HttpPatch("{channel_uuid}/Unarchive")]
        public ActionResult UnarchiveChannel(string channel_uuid)
        {
            string user_uuid = Context.GetUserUUID(this.GetToken());
            if (!ChannelUtils.CheckUserChannelAccess(Context, user_uuid, channel_uuid)) return StatusCode(403, "Permission Denied");
            if (GetChannel(channel_uuid).Value.IsGroup) return StatusCode(405, "Can not Unarchive group");
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();
            using MySqlCommand cmd = new($"UPDATE `{user_uuid}` SET Property=@prop WHERE (Value=@channel)", conn);
            cmd.Parameters.AddWithValue("@prop", "ActiveChannelAccess");
            cmd.Parameters.AddWithValue("@channel", channel_uuid);
            if (cmd.ExecuteNonQuery() > 0) return StatusCode(200);
            return StatusCode(404);
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

        private bool UsersShareChannel(string user_uuid1, string user_uuid2) {
            List<string> user1_channels = GetActiveUserChannels(user_uuid1);
            List<string> user2_channels = GetActiveUserChannels(user_uuid2);
            
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

        private List<string> GetActiveUserChannels(string user_uuid) {
            List<string> channels = new();
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                using MySqlCommand cmd = new($"SELECT * FROM `{user_uuid}` WHERE (Property=@prop)", conn);
                cmd.Parameters.AddWithValue("@prop", "ActiveChannelAccess");
                MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    channels.Add((string)reader["Value"]);
                }
            }
            return channels;
        }

        private List<string> GetArchivedUserChannels(string user_uuid)
        {
            List<string> channels = new();
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                using MySqlCommand cmd = new($"SELECT * FROM `{user_uuid}` WHERE (Property=@prop)", conn);
                cmd.Parameters.AddWithValue("@prop", "ArchivedChannelAccess");
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
