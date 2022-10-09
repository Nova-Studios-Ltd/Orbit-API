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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaAPI.Util;
using System.IO;

namespace NovaAPI.Controllers
{
    [Route("Channel")]
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
        public ActionResult<IEnumerable<ChannelMessage>> GetMessages(string channel_uuid, int limit = 30, int after = -1, int before = int.MaxValue)
        {
            if (!ChannelUtils.CheckUserChannelAccess(Context.GetUserUUID(GetToken()), channel_uuid)) return StatusCode(403, "Access Denied");
            List<ChannelMessage> messages = new();
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Channel))
            {
                // Testing stuff
                conn.Open();
                try
                {
                    MySqlCommand cmd = new($"SELECT * FROM `{channel_uuid}` WHERE Message_ID>{after} AND Message_ID<{before} ORDER BY Message_ID DESC LIMIT {limit}", conn);
                    cmd.Parameters.AddWithValue("@before", before);
                    using MySqlDataReader reader = cmd.ExecuteReader();
                    using MySqlConnection meta = MySqlServer.CreateSQLConnection(Database.Master);
                    meta.Open();
                    while (reader.Read())
                    {
                        List<Attachment> Attachments = new();
                        foreach (string content_id in JsonConvert.DeserializeObject<List<string>>(reader["Attachments"].ToString()))
                        {
                            using MySqlCommand retreiveMeta = new("SELECT * FROM ChannelMedia WHERE (File_UUID=@uuid)", meta);
                            retreiveMeta.Parameters.AddWithValue("@uuid", content_id);
                            MySqlDataReader metaReader = retreiveMeta.ExecuteReader();
                            while (metaReader.Read())
                            {
                                Attachments.Add(new Attachment
                                {
                                    ContentUrl = $"https://{Startup.API_Domain}/Channel/{channel_uuid}/{content_id}",
                                    Filename = metaReader["Filename"].ToString(),
                                    MimeType = metaReader["MimeType"].ToString(),
                                    Size = int.Parse(metaReader["Size"].ToString()),
                                    ContentWidth = int.Parse(metaReader["ContentWidth"].ToString()),
                                    ContentHeight = int.Parse(metaReader["ContentHeight"].ToString()),
                                    Keys = JsonConvert.DeserializeObject<Dictionary<string, string>>(metaReader["User_Keys"].ToString()),
                                    IV = metaReader["IV"].ToString()
                                });
                            }
                            metaReader.Close();
                        }

                        messages.Add(new ChannelMessage
                        {
                            Message_Id = reader["Message_ID"].ToString(),
                            Author_UUID = reader["Author_UUID"].ToString(),
                            Content = reader["Content"].ToString(),
                            IV = reader["IV"].ToString(),
                            EncryptedKeys = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader["EncryptedKeys"].ToString()),
                            Attachments = Attachments,
                            Timestamp = DateTime.Parse(reader["CreationDate"].ToString()),
                            EditedTimestamp = DateTime.Parse(reader["EditedDate"].ToString()),
                            Edited = (bool)reader["Edited"],
                            Avatar = $"https://{Startup.API_Domain}/User/{(reader["Author_UUID"].ToString())}/Avatar?size=64"
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

        [HttpGet("{channel_uuid}/Messages/EditTimestamps")]
        public ActionResult<Dictionary<string, string>> GetMessageHashes(string channel_uuid, int limit = 30, int after = -1, int before = int.MaxValue)
        {
            var m = GetMessages(channel_uuid, limit, after, before).Value;
            if (m == null) return StatusCode(400);
            ChannelMessage[] messages = m.ToArray();
            Dictionary<string, string> hashes = new Dictionary<string, string>();
            foreach (ChannelMessage message in messages)
            {
                hashes.Add(message.Message_Id, message.EditedTimestamp.ToString("s"));
            }
            return hashes;
        }

        [HttpGet("{channel_uuid}/Messages/{message_id}")]
        public ActionResult<ChannelMessage> GetMessage(string channel_uuid, string message_id)
        {
            if (!ChannelUtils.CheckUserChannelAccess(Context.GetUserUUID(GetToken()), channel_uuid)) return StatusCode(403);
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Channel))
            {
                conn.Open();
                try
                {
                    MySqlCommand cmd = new($"SELECT * FROM `{channel_uuid}` WHERE (Message_ID=@uuid)", conn);
                    cmd.Parameters.AddWithValue("@uuid", message_id);
                    using MySqlDataReader reader = cmd.ExecuteReader();
                    using MySqlConnection meta = MySqlServer.CreateSQLConnection(Database.Master);
                    meta.Open();
                    while (reader.Read())
                    {
                        List<Attachment> Attachments = new();
                        foreach (string content_id in JsonConvert.DeserializeObject<List<string>>(reader["Attachments"].ToString()))
                        {
                            using MySqlCommand retreiveMeta = new("SELECT * FROM ChannelMedia WHERE (File_UUID=@uuid)", meta);
                            retreiveMeta.Parameters.AddWithValue("@uuid", content_id);
                            MySqlDataReader metaReader = retreiveMeta.ExecuteReader();
                            while (metaReader.Read())
                            {
                                Attachments.Add(new Attachment
                                {
                                    ContentUrl = $"https://{Startup.API_Domain}/Channel/{channel_uuid}/{content_id}",
                                    Filename = metaReader["Filename"].ToString(),
                                    MimeType = metaReader["MimeType"].ToString(),
                                    Size = int.Parse(metaReader["Size"].ToString()),
                                    ContentWidth = int.Parse(metaReader["ContentWidth"].ToString()),
                                    ContentHeight = int.Parse(metaReader["ContentHeight"].ToString()),
                                    Keys = JsonConvert.DeserializeObject<Dictionary<string, string>>(metaReader["User_Keys"].ToString()),
                                    IV = metaReader["IV"].ToString()
                                });
                            }
                            metaReader.Close();
                        }

                        return new ChannelMessage
                        {
                            Message_Id = reader["Message_ID"].ToString(),
                            Author_UUID = reader["Author_UUID"].ToString(),
                            Content = reader["Content"].ToString(),
                            IV = reader["IV"].ToString(),
                            EncryptedKeys = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader["EncryptedKeys"].ToString()),
                            Attachments = Attachments,
                            Timestamp = DateTime.Parse(reader["CreationDate"].ToString()),
                            EditedTimestamp = DateTime.Parse(reader["EditedDate"].ToString()),
                            Edited = (bool)reader["Edited"],
                            Avatar = $"https://{Startup.API_Domain}/User/{(reader["Author_UUID"].ToString())}/Avatar?size=64"
                        };
                    }
                }
                catch
                {
                    return StatusCode(500);
                }
            }
            return StatusCode(404);
        }

        [HttpPost("{channel_uuid}/Messages/")]
        public ActionResult<string> SendMessage(string channel_uuid, SentMessage message, string contentToken) 
        {
            if (contentToken != "empty" && !TokenManager.ValidToken(contentToken, channel_uuid) && message.Attachments.Count > 0)
            {
                TokenManager.InvalidateToken(contentToken);
                return StatusCode(400, "The provided Content Token has expired");
            }
            
            string user_uuid = Context.GetUserUUID(GetToken());
            string id = "";
            if (!ChannelUtils.CheckUserChannelAccess(user_uuid, channel_uuid)) return StatusCode(403);
            
            // Check friend/blocked status (Ignore groups)
            if (!ChannelUtils.IsGroup(channel_uuid))
            {
                string recip = ChannelUtils.GetRecipents(channel_uuid, user_uuid, true)[0];
                if (!FriendUtils.IsFriend(user_uuid, recip))
                    return StatusCode(403, "Unable to send message to non-friend user");
                if (FriendUtils.IsBlocked(user_uuid, recip))
                    return StatusCode(403, "Unable to send message to blocked user");
            }

            if (message.Content.Length == 0 && message.Attachments.Count == 0) return StatusCode(400, "Message cannot be blank and have 0 attachments");
            
            // Check that attachments arent duplicated and match those of the provided contentToken
            if (message.Attachments.Count != message.Attachments.Distinct().Count())
            {
                TokenManager.InvalidateToken(contentToken);
                return StatusCode(409, "Attachments contains duplicate ids");
            }
            
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Channel))
            {
                conn.Open();
                using MySqlCommand cmd = new($"INSERT INTO `{channel_uuid}` (Author_UUID, Content, Attachments, IV, EncryptedKeys) VALUES (@author, @content, @attachments, @iv, @keys)", conn);
                cmd.Parameters.AddWithValue("@author", user_uuid);
                cmd.Parameters.AddWithValue("@content", message.Content);
                cmd.Parameters.AddWithValue("@attachments", JsonConvert.SerializeObject(message.Attachments));
                cmd.Parameters.AddWithValue("@iv", message.IV);
                cmd.Parameters.AddWithValue("@keys", JsonConvert.SerializeObject(message.EncryptedKeys));
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
            if (!ChannelUtils.CheckUserChannelAccess(user_uuid, channel_uuid)) return StatusCode(403);
            using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Channel))
            {
                conn.Open();
                using MySqlCommand cmd = new($"UPDATE `{channel_uuid}` SET Content=@content,EditedDate=@date,Edited=@edited WHERE (Author_UUID=@author) AND (Message_ID=@message_uuid)", conn);
                cmd.Parameters.AddWithValue("@author", user_uuid);
                cmd.Parameters.AddWithValue("@message_uuid", message_id);
                cmd.Parameters.AddWithValue("@content", message.Content);
                cmd.Parameters.AddWithValue("@date", DateTime.Now);
                cmd.Parameters.AddWithValue("@edited", true);
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
            try
            {
                string user_uuid = Context.GetUserUUID(GetToken());
                if (!ChannelUtils.CheckUserChannelAccess(user_uuid, channel_uuid)) return StatusCode(403);
                using (MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Channel))
                {
                    conn.Open();

                    string[] attachmentUUIDs = null;
                    MySqlCommand getAUUID = new($"SELECT * FROM `{channel_uuid}` WHERE (Message_ID=@uuid)", conn);
                    getAUUID.Parameters.AddWithValue("@uuid", message_id);
                    using MySqlDataReader reader = getAUUID.ExecuteReader();
                    while (reader.Read())
                    {
                        attachmentUUIDs = JsonConvert.DeserializeObject<List<string>>(reader["Attachments"].ToString()).ToArray();
                    }
                    reader.Close();
                    if (attachmentUUIDs == null) attachmentUUIDs = new string[0];

                    foreach (string attachment in attachmentUUIDs)
                    {
                        StorageUtil.DeleteFile(StorageUtil.MediaType.ChannelContent, attachment, channel_uuid);   
                    }

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
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [HttpDelete("{channel_uuid}/Messages/{message_id}/Attachments/{attachment_uuid}")]
        public ActionResult RemoveAttachment(string channel_uuid, string message_id, string attachment_uuid)
        {
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.Channel);
            conn.Open();
            
            List<string> attachmentUUIDs = null;
            MySqlCommand getAUUID = new($"SELECT * FROM `{channel_uuid}` WHERE (Message_ID=@uuid)", conn);
            getAUUID.Parameters.AddWithValue("@uuid", message_id);
            using MySqlDataReader reader = getAUUID.ExecuteReader();
            while (reader.Read())
            {
                attachmentUUIDs = JsonConvert.DeserializeObject<List<string>>(reader["Attachments"].ToString());
            }
            reader.Close();
            if (attachmentUUIDs == null) attachmentUUIDs = new List<string>();

            if (attachmentUUIDs.Contains(attachment_uuid))
            {
                attachmentUUIDs.Remove(attachment_uuid);
                StorageUtil.DeleteFile(StorageUtil.MediaType.ChannelContent, attachment_uuid, channel_uuid);   
                MySqlCommand updateMessageAttachments = new MySqlCommand($"UPDATE `{channel_uuid}` SET Attachments=@atts WHERE (Message_ID=@uuid)", conn);
                updateMessageAttachments.Parameters.AddWithValue("@atts", JsonConvert.SerializeObject(attachmentUUIDs));
                updateMessageAttachments.Parameters.AddWithValue("@uuid", message_id);
                updateMessageAttachments.ExecuteNonQuery();
            }

            return StatusCode(200);
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
