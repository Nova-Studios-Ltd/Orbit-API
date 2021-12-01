using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using NovaAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using NovaAPI.Attri;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaAPI.Util;

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
        public ActionResult<IEnumerable<ChannelMessage>> GetMessages(string channel_uuid, int limit = 30, int before = int.MaxValue)
        {
            if (!ChannelUtils.CheckUserChannelAccess(Context, Context.GetUserUUID(GetToken()), channel_uuid)) return StatusCode(403, "Access Denied");
            List<ChannelMessage> messages = new();
            using (MySqlConnection conn = Context.GetChannels())
            {
                // Testing stuff
                conn.Open();
                try
                {
                    MySqlCommand cmd = new($"SELECT * FROM `{channel_uuid}` WHERE Message_ID<{before} ORDER BY Message_ID DESC LIMIT {limit}", conn);
                    cmd.Parameters.AddWithValue("@before", before);
                    using MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        messages.Add(new ChannelMessage
                        {
                            Message_Id = reader["Message_ID"].ToString(),
                            Author = Context.GetUserUsername(reader["Author_UUID"].ToString()),
                            Author_UUID = reader["Author_UUID"].ToString(),
                            Content = reader["Content"].ToString(),
                            Attachments = JsonConvert.DeserializeObject<List<string>>(reader["Attachments"].ToString()),
                            Timestamp = DateTime.Parse(reader["CreationDate"].ToString()),
                            Avatar = $"https://api.novastudios.tk/Media/Avatar/{(reader["Author_UUID"].ToString())}?size=64"
                        });
                    }
                }
                catch (Exception e)
                {
                    return StatusCode(404, $"Channel \"{channel_uuid}\" is not available {e.Message}");
                }
            }
            return messages;
        }

        [HttpGet("{channel_uuid}/Messages/{message_id}")]
        public ActionResult<ChannelMessage> GetMessage(string channel_uuid, string message_id)
        {
            if (!ChannelUtils.CheckUserChannelAccess(Context, Context.GetUserUUID(GetToken()), channel_uuid)) return StatusCode(403);
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                try
                {
                    MySqlCommand cmd = new($"SELECT * FROM `{channel_uuid}` WHERE (Message_ID=@uuid)", conn);
                    cmd.Parameters.AddWithValue("@uuid", message_id);
                    using MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        return new ChannelMessage
                        {
                            Message_Id = reader["Message_ID"].ToString(),
                            Author = Context.GetUserUsername(reader["Author_UUID"].ToString()),
                            Author_UUID = reader["Author_UUID"].ToString(),
                            Content = reader["Content"].ToString(),
                            Timestamp = DateTime.Parse(reader["CreationDate"].ToString()),
                            Avatar = $"https://api.novastudios.tk/Media/Avatar/{(reader["Author_UUID"].ToString())}?size=64"
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
            string user_uuid = Context.GetUserUUID(GetToken());
            string id = "";
            if (!ChannelUtils.CheckUserChannelAccess(Context, user_uuid, channel_uuid)) return StatusCode(403);
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                using MySqlCommand cmd = new($"INSERT INTO `{channel_uuid}` (Author_UUID, Content, Attachments) VALUES (@author, @content, @attachments)", conn);
                cmd.Parameters.AddWithValue("@author", user_uuid);
                cmd.Parameters.AddWithValue("@content", message.Content);
                cmd.Parameters.AddWithValue("@attachments", JsonConvert.SerializeObject(message.Attachments));
                cmd.ExecuteNonQuery();
                
                using MySqlCommand getId = new($"SELECT Message_ID FROM `{channel_uuid}` ORDER BY Message_ID DESC LIMIT 1", conn);
                MySqlDataReader reader = getId.ExecuteReader();
                while (reader.Read()) {
                    id = reader["Message_ID"].ToString();
                }
            }
            Event.MessageSentEvent(channel_uuid, id);
            return id;
        }

        [HttpPut("{channel_uuid}/Messages/{message_id}")]
        public ActionResult EditMessage(string channel_uuid, string message_id, SentMessage message)
        {
            string user_uuid = Context.GetUserUUID(GetToken());
            if (!ChannelUtils.CheckUserChannelAccess(Context, user_uuid, channel_uuid)) return StatusCode(403);
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                using MySqlCommand cmd = new($"UPDATE `{channel_uuid}` SET Content=@content WHERE (Author_UUID=@author) AND (Message_ID=@message_uuid)", conn);
                cmd.Parameters.AddWithValue("@author", user_uuid);
                cmd.Parameters.AddWithValue("@message_uuid", message_id);
                cmd.Parameters.AddWithValue("@content", message.Content);
                if (cmd.ExecuteNonQuery() > 0)
                {
                    Event.MessageEditEvent(channel_uuid, message_id);
                    return StatusCode(200);
                }
            }
            return StatusCode(404, "Unknown/Unallowed access to message");
        }

        [HttpDelete("{channel_uuid}/Messages/{message_id}")]
        public ActionResult DeleteMessage(string channel_uuid, string message_id)
        {
            string user_uuid = Context.GetUserUUID(GetToken());
            if (!ChannelUtils.CheckUserChannelAccess(Context, user_uuid, channel_uuid)) return StatusCode(403);
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                using MySqlCommand cmd = new($"DELETE FROM `{channel_uuid}` WHERE (Message_ID=@message_uuid) AND (Author_UUID=@user_uuid)", conn);
                cmd.Parameters.AddWithValue("@channel_uuid", channel_uuid);
                cmd.Parameters.AddWithValue("@message_uuid", message_id);
                cmd.Parameters.AddWithValue("@user_uuid", user_uuid);
                if (cmd.ExecuteNonQuery() > 0)
                {
                    Event.MessageDeleteEvent(channel_uuid, message_id);
                    return StatusCode(200);
                }
            }
            return StatusCode(404, "Unknown/Unallowed access to message");
        }

        [HttpPost("TriggerMessageEvent")]
        public ActionResult MessageEvent(string channel_uuid, string message_id, int eventType)
        {
            if (eventType == 0) Event.MessageSentEvent(channel_uuid, message_id);
            if (eventType == 1) Event.MessageDeleteEvent(channel_uuid, message_id);
            return StatusCode(200);
        }

        string GetToken()
        {
            if (!Request.Headers.TryGetValue("Authorization", out StringValues values))
                return "";
            return values.First();
        }
    }
}
