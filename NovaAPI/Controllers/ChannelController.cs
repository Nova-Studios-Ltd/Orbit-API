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
                using MySqlCommand addChannel = new($"INSERT INTO `Channels` (`Table_ID`, `Owner_UUID`, `Timestamp`) VALUES (@table_id, @owner_uuid, CURRENT_TIMESTAMP)", conn);
                addChannel.Parameters.AddWithValue("@table_id", table_id);
                addChannel.Parameters.AddWithValue("@owner_uuid", Context.GetUserUUID(this.GetToken()));
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

        //[HttpPost("CreateGroupChannel")]
        //public ActionResult<string> CreateGroupChannel(List<string> recipients)
        //{

        //}

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
                conn.Open();

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
    }
}
