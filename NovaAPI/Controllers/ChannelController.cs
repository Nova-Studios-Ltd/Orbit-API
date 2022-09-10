using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using NovaAPI.Attri;
using NovaAPI.Models;
using NovaAPI.Util;

namespace NovaAPI.Controllers
{
    public enum ChannelTypes
    {
        DMChannel,
        GroupChannel,
        PrivateChannel
    }

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
            if (!FriendUtils.IsFriend(author, recipient_uuid))
                return StatusCode(403, "Unable to create channel with non-friend users");
            if (FriendUtils.IsBlocked(author, recipient_uuid))
                return StatusCode(403, "Unable to create channel with blocked users");

            string table_id = Guid.NewGuid().ToString("N");
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Channel))
            {
                conn.Open();

                // Create table to hold sent messages
                using MySqlCommand createTable = new($"CREATE TABLE `{table_id}` (Message_ID BIGINT NOT NULL AUTO_INCREMENT, Author_UUID CHAR(255) NOT NULL, Content VARCHAR(4000) NOT NULL, Attachments JSON NOT NULL, IV VARCHAR(1000) NOT NULL, EncryptedKeys JSON NOT NULL, EditedDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, Edited BOOLEAN NOT NULL DEFAULT FALSE, CreationDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, PRIMARY KEY (Message_ID)) ENGINE = InnoDB;", conn);
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
                using MySqlConnection cTable = MySqlServer.CreateSQLConnection(Database.Master);
                cTable.Open();
                using MySqlCommand addChannel = new($"INSERT INTO `Channels` (`Table_ID`, `Owner_UUID`, `ChannelIcon`, `Timestamp`) VALUES (@table_id, @owner_uuid, @icon, CURRENT_TIMESTAMP)", cTable);
                addChannel.Parameters.AddWithValue("@table_id", table_id);
                addChannel.Parameters.AddWithValue("@owner_uuid", Context.GetUserUUID(this.GetToken()));
                addChannel.Parameters.AddWithValue("@icon", "");
                addChannel.ExecuteNonQuery();
            }

            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User))
            {
                conn.Open();

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

                // Exchange pub keys
                try
                {
                    using MySqlCommand ownerExchangeKey = new($"INSERT INTO `{Context.GetUserUUID(this.GetToken())}_keystore` (UUID, PubKey) VALUES (@uuid, @key)", conn);
                    ownerExchangeKey.Parameters.AddWithValue("@uuid", recipient_uuid);
                    ownerExchangeKey.Parameters.AddWithValue("@key", Context.GetUserPubKey(recipient_uuid));
                    ownerExchangeKey.ExecuteNonQuery();
                }
                catch { }

                try
                {
                    using MySqlCommand recipientExchangeKey = new($"INSERT INTO `{recipient_uuid}_keystore` (UUID, PubKey) VALUES (@uuid, @key)", conn);
                    recipientExchangeKey.Parameters.AddWithValue("@uuid", Context.GetUserUUID(this.GetToken()));
                    recipientExchangeKey.Parameters.AddWithValue("@key", Context.GetUserPubKey(Context.GetUserUUID(this.GetToken())));
                    recipientExchangeKey.ExecuteNonQuery();
                }
                catch { }

                Event.KeyAddedToKeystore(Context.GetUserUUID(this.GetToken()), recipient_uuid);
                Event.KeyAddedToKeystore(recipient_uuid, Context.GetUserUUID(this.GetToken()));

                cmd.Dispose();
            }
            
            Event.ChannelCreatedEvent(table_id);
            return table_id;
        }

        // Create private "transfer" channel, used for sending content between devices, doesnt allow more than yoursefl in it
        [HttpPost("CreatePrivate")]
        public ActionResult<string> CreatePrivate()
        {
            string author = Context.GetUserUUID(this.GetToken());
            string table_id = Guid.NewGuid().ToString("N");
            
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Channel))
            {
                conn.Open();

                // Create table to hold sent messages
                using MySqlCommand createTable = new($"CREATE TABLE `{table_id}` (Message_ID BIGINT NOT NULL AUTO_INCREMENT, Author_UUID CHAR(255) NOT NULL, Content VARCHAR(4000) NOT NULL, Attachments JSON NOT NULL, IV VARCHAR(1000) NOT NULL, EncryptedKeys JSON NOT NULL, EditedDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, Edited BOOLEAN NOT NULL DEFAULT FALSE, CreationDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, PRIMARY KEY (Message_ID)) ENGINE = InnoDB;", conn);
                createTable.ExecuteNonQuery();

                // Create table to hold the users attached to this channel
                using MySqlCommand createAccessTable = new($"CREATE TABLE `access_{table_id}` (User_UUID CHAR(255) NOT NULL, DELETED BOOLEAN NOT NULL DEFAULT FALSE, UNIQUE User_UUIDs (User_UUID)) ENGINE = InnoDB;", conn);
                createAccessTable.ExecuteNonQuery();

                // Add users to access table
                using MySqlCommand addUserAccess = new($"INSERT INTO  `access_{table_id}` (User_UUID) VALUES (@author), (@recipient)", conn);
                addUserAccess.Parameters.AddWithValue("@author", author);
                addUserAccess.ExecuteNonQuery();

                // Add table id to channels table
                using MySqlConnection cTable = MySqlServer.CreateSQLConnection(Database.Master);
                cTable.Open();
                using MySqlCommand addChannel = new($"INSERT INTO `Channels` (`Table_ID`, `Owner_UUID`, `ChannelType`, `ChannelIcon`, `Timestamp`) VALUES (@table_id, @owner_uuid, @type, @icon, CURRENT_TIMESTAMP)", cTable);
                addChannel.Parameters.AddWithValue("@table_id", table_id);
                addChannel.Parameters.AddWithValue("@owner_uuid", Context.GetUserUUID(this.GetToken()));
                addChannel.Parameters.AddWithValue("@type", ChannelTypes.PrivateChannel);
                addChannel.Parameters.AddWithValue("@icon", "");
                addChannel.ExecuteNonQuery();
            }

            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User))
            {
                conn.Open();

                // Add channel to author
                MySqlCommand cmd  = new($"INSERT INTO `{Context.GetUserUUID(this.GetToken())}` (Property, Value) VALUES (@property, @uuid)", conn);
                cmd.Parameters.AddWithValue("@property", "ActiveChannelAccess");
                cmd.Parameters.AddWithValue("@uuid", table_id);
                cmd.ExecuteNonQuery();
                cmd.Dispose();
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
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Channel))
            {
                conn.Open();

                // Create table to hold sent messages
                using MySqlCommand createTable = new($"CREATE TABLE `{table_id}` (Message_ID BIGINT NOT NULL AUTO_INCREMENT, Author_UUID CHAR(255) NOT NULL , Content VARCHAR(4000) NOT NULL , Attachments JSON NOT NULL, IV VARCHAR(1000) NOT NULL, EncryptedKeys JSON NOT NULL, EditedDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, Edited BOOLEAN NOT NULL DEFAULT FALSE, CreationDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP , PRIMARY KEY (Message_ID)) ENGINE = InnoDB;", conn);
                createTable.ExecuteNonQuery();

                // Create table to hold the users attached to this channel
                using MySqlCommand createAccessTable = new($"CREATE TABLE `access_{table_id}` (User_UUID CHAR(255) NOT NULL, DELETED BOOLEAN NOT NULL DEFAULT FALSE, UNIQUE User_UUIDs (User_UUID)) ENGINE = InnoDB;", conn);
                createAccessTable.ExecuteNonQuery();

                // Add users to access table
                foreach (string recipient in recipients)
                {
                    if (string.IsNullOrEmpty(recipient) || !Context.UserExsists(recipient)) continue;
                    using MySqlCommand addUserAccess = new($"INSERT INTO `access_{table_id}` (User_UUID) VALUES (@recipient)", conn);
                    addUserAccess.Parameters.AddWithValue("@recipient", recipient);
                    addUserAccess.ExecuteNonQuery();
                }

                using MySqlCommand addAuthor = new($"INSERT INTO  `access_{table_id}` (User_UUID) VALUES (@recipient)", conn);
                addAuthor.Parameters.AddWithValue("@recipient", Context.GetUserUUID(this.GetToken()));
                addAuthor.ExecuteNonQuery();

                // Add table id to channels table
                using MySqlConnection cTable = MySqlServer.CreateSQLConnection(Database.Master);
                cTable.Open();
                using MySqlCommand addChannel = new($"INSERT INTO `Channels` (`Table_ID`, `Owner_UUID`, `ChannelIcon`, `ChannelType`, `Timestamp`, `GroupName`) VALUES (@table_id, @owner_uuid, @icon, @type, CURRENT_TIMESTAMP, @gn)", cTable);
                addChannel.Parameters.AddWithValue("@table_id", table_id);
                addChannel.Parameters.AddWithValue("@owner_uuid", Context.GetUserUUID(this.GetToken()));
                addChannel.Parameters.AddWithValue("@icon", "");
                addChannel.Parameters.AddWithValue("@type", ChannelTypes.GroupChannel);
                addChannel.Parameters.AddWithValue("@gn", group_name);
                addChannel.ExecuteNonQuery();
            }

            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User))
            {
                conn.Open();
                foreach (string recipient in recipients)
                {
                    if (string.IsNullOrEmpty(recipient) || !Context.UserExsists(recipient)) continue;
                    // Add channel to receiver
                    MySqlCommand addToRec = new($"INSERT INTO `{recipient}` (Property, Value) VALUES (@property, @uuid)", conn);
                    addToRec.Parameters.AddWithValue("@property", "ActiveChannelAccess");
                    addToRec.Parameters.AddWithValue("@uuid", table_id);
                    addToRec.ExecuteNonQuery();

                    // Send pub keys
                    //SendPubKey(recipient, recipients, Context.GetUserPubKey(recipient));
                    foreach (string r in recipients)
                    {
                        if (r != recipient)
                            Context.SetUserPubKey(r, recipient, Context.GetUserPubKey(recipient));
                    }

                    // Send all others keys to the author
                    Context.SetUserPubKey(Context.GetUserUUID(this.GetToken()), recipient, Context.GetUserPubKey(recipient));
                }

                // Add channel to author
                MySqlCommand cmd = new($"INSERT INTO `{author}` (Property, Value) VALUES (@property, @uuid)", conn);
                cmd.Parameters.AddWithValue("@property", "ActiveChannelAccess");
                cmd.Parameters.AddWithValue("@uuid", table_id);
                if (cmd.ExecuteNonQuery() == 0)
                {
                    RemoveChannel(table_id);
                    return StatusCode(500);
                }

                // Send author's pub key
                SendPubKey(Context.GetUserUUID(this.GetToken()), recipients, Context.GetUserPubKey(Context.GetUserUUID(this.GetToken())));

                cmd.Dispose();
            }
            // Refresh keystores
            foreach (string r in recipients)
            {
                Event.RefreshKeystore(r);
            }

            Event.RefreshKeystore(author);

            Event.ChannelCreatedEvent(table_id);
            return table_id;
        }

        [HttpPatch("{channel_uuid}/Members")]
        public ActionResult AddUserToGroupChannel(string channel_uuid, List<string> recipients) 
        {
            if (!ChannelUtils.CheckUserChannelAccess(Context.GetUserUUID(this.GetToken()), channel_uuid)) return StatusCode(403);
            Channel c = GetChannel(channel_uuid).Value;
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User))
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

            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Master))
            {
                conn.Open();
                if (c.ChannelType == ChannelTypes.DMChannel && c.Members.Count > 2) {
                    using MySqlCommand updateType = new($"UPDATE Channels SET ChannelType=@isGroup WHERE (Table_ID=@channel_uuid)", conn);
                    updateType.Parameters.AddWithValue("@isGroup", ChannelTypes.GroupChannel);
                    updateType.Parameters.AddWithValue("@channel_uuid", channel_uuid);
                    updateType.ExecuteNonQuery();
                }

                // Add user to channel
                using MySqlConnection cTable = MySqlServer.CreateSQLConnection(Database.Channel);
                cTable.Open();
                foreach (string recipient in recipients)
                {
                    if (string.IsNullOrEmpty(recipient) || !Context.UserExsists(recipient)) continue;
                    Event.UserNewGroup(channel_uuid, recipient);
                    using MySqlCommand cmd = new($"INSERT INTO `access_{channel_uuid}` (User_UUID) VALUES (@uuid)", cTable);
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
            if (c.ChannelType != ChannelTypes.GroupChannel) return StatusCode(405);
            
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Master))
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

            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Channel))
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
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Master)) {
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
            if (!ChannelUtils.CheckUserChannelAccess(user_uuid, channel_uuid, true)) return StatusCode(403);
            Channel channel = new();
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Master))
            {
                conn.Open();
                MySqlCommand retreiveChannel = new($"SELECT * FROM Channels WHERE (Table_ID=@table_id)", conn);
                retreiveChannel.Parameters.AddWithValue("@table_id", channel_uuid);
                MySqlDataReader reader = retreiveChannel.ExecuteReader();
                while (reader.Read())
                {
                    channel.Table_Id = channel_uuid;
                    channel.Owner_UUID = (string)reader["Owner_UUID"];
                    channel.ChannelType = (ChannelTypes)(int)((sbyte)reader["ChannelType"] & 0xff);
                    channel.ChannelName = (string)reader["GroupName"];
                    channel.ChannelIcon = $"https://api.novastudios.tk/Channel/{channel_uuid}/Icon?size=64";
                        
                }
                reader.Close();
                using MySqlConnection cTable = MySqlServer.CreateSQLConnection(Database.Channel);
                cTable.Open();
                MySqlCommand retreiveMembers = new($"SELECT * FROM `access_{channel_uuid}`", cTable);
                reader = retreiveMembers.ExecuteReader();
                channel.Members = new();
                while (reader.Read())
                {
                    string member = (string)reader["User_UUID"];
                    if (!(bool)reader["DELETED"])
                        channel.Members.Add(member);
                    if (channel.ChannelType == ChannelTypes.DMChannel)
                    {
                        if (member == user_uuid) continue;
                        channel.ChannelName = Context.GetUserUsername(member);
                        channel.ChannelIcon = $"https://api.novastudios.tk/User/{member}/Avatar?size=64";
                    }
                }
            }
            return channel;
        }

        [HttpDelete("{channel_uuid}")]
        public ActionResult RemoveChannel(string channel_uuid)
        {
            string user_uuid = Context.GetUserUUID(this.GetToken());
            if (!ChannelUtils.CheckUserChannelAccess(user_uuid, channel_uuid)) return StatusCode(403, "Permission Denied");

            Channel channel = GetChannel(channel_uuid).Value;

            // Handle Standard Channel
            if (channel.ChannelType == ChannelTypes.DMChannel)
            {
                // Set channel to DeletedChannel in use props
                SetUserDeletedChannel(channel_uuid, user_uuid, true);

                // Get Updated information about channel
                channel = GetChannel(channel_uuid).Value;

                if (channel.Members.Count <= 1)
                {
                    // Remove Access Table and Chat History
                    using MySqlConnection channelCon = MySqlServer.CreateSQLConnection(Database.Channel);
                    channelCon.Open();
                    using MySqlCommand removeChannel = new($"DROP TABLE `{channel_uuid}`, `access_{channel_uuid}`", channelCon);
                    removeChannel.ExecuteNonQuery();
                  
                    // Remove Channel
                    using MySqlConnection cTable = MySqlServer.CreateSQLConnection(Database.Master);
                    cTable.Open();
                    using MySqlCommand cmd = new($"DELETE FROM Channels WHERE (Table_ID=@table_id)", cTable);
                    cmd.Parameters.AddWithValue("@table_id", channel_uuid);
                    if (cmd.ExecuteNonQuery() == 0) return NotFound();
                    channelCon.Close();

                    foreach (string member in channel.Members)
                    {
                        RemoveChannelFromUser(channel_uuid, member);
                    }
                    
                    Event.ChannelDeleteEvent(channel_uuid, user_uuid);

                    StorageUtil.RemoveChannelContent(channel_uuid);

                    return StatusCode(200, "Channel Removed");
                }
                else
                {
                    // Set user to deleted in access table
                    using MySqlConnection channelCon = MySqlServer.CreateSQLConnection(Database.Channel);
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
                    using MySqlConnection channelCon = MySqlServer.CreateSQLConnection(Database.Master);
                    channelCon.Open();
                    using MySqlCommand removeChannel = new($"DELETE FROM Channels WHERE (Table_ID=@table_id) AND (Owner_UUID=@owner_uuid)", channelCon);
                    removeChannel.Parameters.AddWithValue("@table_id", channel_uuid);
                    removeChannel.Parameters.AddWithValue("@owner_uuid", user_uuid);
                    if (removeChannel.ExecuteNonQuery() == 0) return StatusCode(404);

                    // Get all users with access
                    using MySqlConnection cTable = MySqlServer.CreateSQLConnection(Database.Channel);
                    cTable.Open();
                    using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", cTable);
                    MySqlDataReader reader = removeUsers.ExecuteReader();
                    using MySqlConnection userDb = MySqlServer.CreateSQLConnection(Database.User);
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
                    reader.Close();

                    using MySqlCommand removeChannelTables = new($"DROP TABLE `{channel_uuid}`, `access_{channel_uuid}`", channelCon);
                    removeChannelTables.ExecuteNonQuery();
                    channelCon.Close();
                    
                    Event.ChannelDeleteEvent(channel_uuid);

                    StorageUtil.RemoveChannelContent(channel_uuid);

                    return StatusCode(200, "Group Removed");
                }
                else
                {
                    // Delete channel from User props table
                    using MySqlConnection userDb = MySqlServer.CreateSQLConnection(Database.Channel);
                    userDb.Open();
                    using MySqlCommand removeAccess = new($"DELETE FROM `{user_uuid}` WHERE (Property=@prop) AND (Value=@value)", userDb);
                    removeAccess.Parameters.AddWithValue("@prop", "ActiveChannelAccess");
                    removeAccess.Parameters.AddWithValue("@value", channel_uuid);
                    removeAccess.ExecuteNonQuery();
                    userDb.Close();

                    // Delete user from Channel Access table
                    using MySqlConnection channelsDB = MySqlServer.CreateSQLConnection(Database.Channel);
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
            if (!ChannelUtils.CheckUserChannelAccess(user_uuid, channel_uuid)) return StatusCode(403, "Permission Denied");
            if (GetChannel(channel_uuid).Value.ChannelType == ChannelTypes.GroupChannel) return StatusCode(405, "Can not Archive group");
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User);
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
            if (!ChannelUtils.CheckUserChannelAccess(user_uuid, channel_uuid)) return StatusCode(403, "Permission Denied");
            if (GetChannel(channel_uuid).Value.ChannelType == ChannelTypes.GroupChannel) return StatusCode(405, "Can not Unarchive group");
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User);
            conn.Open();
            using MySqlCommand cmd = new($"UPDATE `{user_uuid}` SET Property=@prop WHERE (Value=@channel)", conn);
            cmd.Parameters.AddWithValue("@prop", "ActiveChannelAccess");
            cmd.Parameters.AddWithValue("@channel", channel_uuid);
            if (cmd.ExecuteNonQuery() > 0) return StatusCode(200);
            return StatusCode(404);
        }


        bool CheckUserChannelOwner(string channel_uuid, string user_uuid) 
        {
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Master);
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

        private bool UsersShareChannel(string user_uuid1, string user_uuid2)
        {
            List<string> user1_channels = GetActiveUserChannels(user_uuid1);
            user1_channels.AddRange(GetDeletedUserChannels(user_uuid1));
            List<string> user2_channels = GetActiveUserChannels(user_uuid2);
            user2_channels.AddRange(GetDeletedUserChannels(user_uuid2));
            
            string[] matchingChannels = user1_channels.Intersect(user2_channels).ToArray();

            foreach (string channel in matchingChannels)
            {
                Channel c = GetChannel(channel).Value;
                if (c == null) continue;
                if (c.ChannelType == ChannelTypes.DMChannel)
                {
                    if (c.Members.Contains(user_uuid1) && c.Members.Contains(user_uuid2))
                    {
                        SetChannelDeleteStatus(c.Table_Id, user_uuid1, false);
                        SetChannelDeleteStatus(c.Table_Id, user_uuid2, false);
                        SetUserDeletedChannel(c.Table_Id, user_uuid1, false);
                        SetUserDeletedChannel(c.Table_Id, user_uuid2, false);
                        Event.ChannelCreatedEvent(c.Table_Id);
                        return true;
                    }
                }
            }
            return false;
        }

        private List<string> GetActiveUserChannels(string user_uuid) {
            List<string> channels = new();
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User))
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

        private List<string> GetDeletedUserChannels(string user_uuid)
        {
            List<string> channels = new();
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User))
            {
                conn.Open();
                using MySqlCommand cmd = new($"SELECT * FROM `{user_uuid}` WHERE (Property=@prop)", conn);
                cmd.Parameters.AddWithValue("@prop", "DeletedChannelAccess");
                MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    channels.Add((string)reader["Value"]);
                }
            }
            return channels;
        }

        private void SetChannelDeleteStatus(string channel_uuid, string user_uuid, bool status)
        {
            using MySqlConnection channelCon = MySqlServer.CreateSQLConnection(Database.Channel);
            channelCon.Open();
            using MySqlCommand updateAccess = new($"UPDATE `access_{channel_uuid}` SET DELETED=@status WHERE (User_UUID=@uuid)", channelCon);
            updateAccess.Parameters.AddWithValue("@status", status);
            updateAccess.Parameters.AddWithValue("@uuid", user_uuid);
            if (updateAccess.ExecuteNonQuery() == 0) return;
            channelCon.Close();
        }

        private void SetUserDeletedChannel(string channel_uuid, string user_uuid, bool status)
        {
            using MySqlConnection userConn = MySqlServer.CreateSQLConnection(Database.User);
            userConn.Open();
            using MySqlCommand updateChannel = new($"UPDATE `{user_uuid}` SET Property=@prop WHERE Value=@channel", userConn);
            updateChannel.Parameters.AddWithValue("@prop", (status) ? "DeletedChannelAccess" : "ActiveChannelAccess");
            updateChannel.Parameters.AddWithValue("@channel", channel_uuid);
            updateChannel.ExecuteNonQuery();
            
            userConn.Close();
        }

        private void RemoveChannelFromUser(string channel_uuid, string user_uuid)
        {
            using MySqlConnection userConn = MySqlServer.CreateSQLConnection(Database.User);
            userConn.Open();
            using MySqlCommand removeFromUser = new($"DELETE FROM `{user_uuid}` WHERE (Value=@channel)", userConn);
            removeFromUser.Parameters.AddWithValue("@channel", channel_uuid);
            Console.WriteLine(removeFromUser.CommandText);
            removeFromUser.ExecuteNonQuery();
            userConn.Close();
        }
        
        private List<string> GetArchivedUserChannels(string user_uuid)
        {
            List<string> channels = new();
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User))
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

        private void SendPubKey(string user_uuid, List<string> recipient, string key)
        {
            foreach (string r in recipient)
            {
                if (r == user_uuid) continue;
                Context.SetUserPubKey(user_uuid, r, key);
            }
        }
    }
}
