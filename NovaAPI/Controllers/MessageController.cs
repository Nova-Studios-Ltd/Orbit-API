using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using NovaAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NovaAPI.Attri;
using Microsoft.Extensions.Primitives;

namespace NovaAPI.Controllers
{
    [Route("Message")]
    [ApiController]
    [TokenAuthorization]
    public class MessageController : ControllerBase
    {
        private readonly NovaChatDatabaseContext Context;
        private readonly EventManager Event;
        public MessageController(NovaChatDatabaseContext context, EventManager em)
        {
            Context = context;
            Event = em;
        }

        [HttpGet("{channel_uuid}/Messages/")]
        public ActionResult<IEnumerable<ChannelMessage>> GetMessages(string channel_uuid)
        {
            if (!CheckUserChannelAccess(Context.GetUserUUID(GetToken()), channel_uuid)) return StatusCode(403);
            List<ChannelMessage> messages = new();
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                try
                {
                    MySqlCommand cmd = new($"SELECT * FROM {channel_uuid} ORDER BY CreationDate ASC LIMIT 30", conn);
                    using MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        messages.Add(new ChannelMessage
                        {
                            Message_Id = reader["Message_UUID"].ToString(),
                            Author = Context.GetUserUsername(reader["Author_UUID"].ToString()),
                            Author_UUID = reader["Author_UUID"].ToString(),
                            Content = reader["Content"].ToString(),
                            Timestamp = DateTime.Parse(reader["CreationDate"].ToString())
                        });
                    }
                }
                catch
                {
                    return NotFound();
                }
            }
            return messages;
        }

        [HttpGet("{channel_uuid}/Messages/{message_uuid}")]
        public ActionResult<ChannelMessage> GetMessage(string channel_uuid, string message_uuid)
        {
            if (!CheckUserChannelAccess(Context.GetUserUUID(GetToken()), channel_uuid)) return StatusCode(403);
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                try
                {
                    MySqlCommand cmd = new($"SELECT * FROM {channel_uuid} WHERE (Message_UUID=@uuid)", conn);
                    cmd.Parameters.AddWithValue("@uuid", message_uuid);
                    using MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        return new ChannelMessage
                        {
                            Message_Id = reader["Message_UUID"].ToString(),
                            Author = Context.GetUserUsername(reader["Author_UUID"].ToString()),
                            Author_UUID = reader["Author_UUID"].ToString(),
                            Content = reader["Content"].ToString(),
                            Timestamp = DateTime.Parse(reader["CreationDate"].ToString())
                        };
                    }
                }
                catch
                {
                    return StatusCode(404);
                }
            }
            return StatusCode(500);
        }

        [HttpPost("{channel_uuid}/Messages/")]
        public ActionResult<string> SendMessage(string channel_uuid, SentMessage message)
        {
            string UUID = Guid.NewGuid().ToString("N");
            string user = Context.GetUserUUID(GetToken());
            if (!CheckUserChannelAccess(user, channel_uuid)) return StatusCode(403);
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                using MySqlCommand cmd = new($"INSERT INTO {channel_uuid} (Message_UUID, Author_UUID, Content) VALUES (@message_id, @author, @content)", conn);
                cmd.Parameters.AddWithValue("@message_id", UUID);
                cmd.Parameters.AddWithValue("@author", user);
                cmd.Parameters.AddWithValue("@content", message.Content);
                cmd.ExecuteNonQuery();
            }
            Event.MessageSentEvent(channel_uuid, UUID);
            return UUID;
        }

        [HttpPut("{channel_uuid}/Messages/{message_uuid}")]
        public ActionResult<object> EditMessage(string channel_uuid, string message_uuid, SentMessage message)
        {
            string user = Context.GetUserUUID(GetToken());
            if (!CheckUserChannelAccess(user, channel_uuid)) return StatusCode(403);
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                using MySqlCommand cmd = new($"UPDATE {channel_uuid} SET Content=@content WHERE (Author_UUID=@author) AND (Message_UUID=@message_uuid)", conn);
                cmd.Parameters.AddWithValue("@author", user);
                cmd.Parameters.AddWithValue("@message_uuid", message_uuid);
                cmd.Parameters.AddWithValue("@content", message.Content);
                cmd.ExecuteNonQuery();
            }
            return StatusCode(200);
        }

        [HttpDelete("{channel_uuid}/Messages/{message_uuid}")]
        public ActionResult DeleteMessage(string channel_uuid, string message_uuid)
        {
            if (!CheckUserChannelAccess(Context.GetUserUUID(GetToken()), channel_uuid)) return StatusCode(403);
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                using MySqlCommand cmd = new($"DELETE FROM {channel_uuid} WHERE Message_UUID=@message_uuid", conn);
                cmd.Parameters.AddWithValue("@channel_uuid", channel_uuid);
                cmd.Parameters.AddWithValue("@message_uuid", message_uuid);
                cmd.ExecuteNonQuery();
            }
            return StatusCode(200);
        }

        string GetToken()
        {
            if (!Request.Headers.TryGetValue("Authorization", out StringValues values))
                return "";
            return values.First();
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
