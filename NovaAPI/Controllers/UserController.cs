using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using NovaAPI;
using NovaAPI.Models;
using NovaAPI.Attri;
using Microsoft.Extensions.Primitives;
using NovaAPI.Util;
using System.IO;

namespace NovaAPI.Controllers
{
    [Route("User")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly NovaChatDatabaseContext Context;

        public UserController(NovaChatDatabaseContext context)
        {
            Context = context;
        }

        [HttpGet("{user_uuid}")]
        [TokenAuthorization]
        public ActionResult<object> GetUser(string user_uuid)
        {
            object user = null;
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                if (user_uuid == Context.GetUserUUID(this.GetToken()))
                {
                    MySqlCommand cmd = new($"SELECT * FROM Users WHERE (UUID=@uuid) AND (Token=@token)", conn);
                    cmd.Parameters.AddWithValue("@uuid", user_uuid);
                    cmd.Parameters.AddWithValue("@token", this.GetToken());
                    using MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        user = new
                        {
                            UUID = reader["UUID"].ToString(),
                            Username = reader["Username"].ToString(),
                            Discriminator = reader["Discriminator"].ToString(),
                            Email = reader["Email"].ToString(),
                            CreationDate = DateTime.Parse(reader["CreationDate"].ToString()),
                            Avatar = $"https://api.novastudios.tk/Media/Avatar/${(reader["UUID"].ToString())}?size=64"
                        };
                    }
                }
                else
                {
                    MySqlCommand cmd = new($"SELECT * FROM Users WHERE (UUID=@uuid)", conn);
                    cmd.Parameters.AddWithValue("@uuid", user_uuid);
                    using MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        user = new
                        {
                            UUID = reader["UUID"].ToString(),
                            Username = reader["Username"].ToString(),
                            Discriminator = reader["Discriminator"].ToString(),
                            CreationDate = DateTime.Parse(reader["CreationDate"].ToString()),
                            Avatar = $"https://api.novastudios.tk/Media/Avatar/${(reader["UUID"].ToString())}?size=64"
                        };
                    }
                }
            }

            if (user == null)
            {
                return StatusCode(404);
            }

            return user;
        }

        [HttpPost("/Login")]
        public ActionResult<object> LoginUser(LoginUserInfo info)
        {
            try
            {
                using (MySqlConnection conn = Context.GetUsers())
                {
                    conn.Open();
                    MySqlCommand cmd = new($"SELECT * FROM Users WHERE (Email=@email)", conn);
                    cmd.Parameters.AddWithValue("@email", info.Email);
                    using MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        if (reader["Password"].ToString() == GetHashString(info.Password))
                            return new
                            {
                                UUID = reader["UUID"].ToString(),
                                Token = reader["Token"].ToString()
                            };
                        else
                            return StatusCode(403);
                    }
                }
                return StatusCode(404);
            }
            catch
            {
                return StatusCode(500);
            }
        }

        [HttpPatch("{user_uuid}")]
        [TokenAuthorization]
        public ActionResult UpdateUser(string user_uuid, UpdateType updateType, [FromBody] string data)
        {
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                if (string.IsNullOrEmpty(data)) return StatusCode(400);
                if (updateType == UpdateType.Username)
                {
                    using MySqlCommand cmd = new($"UPDATE Users SET Username=@user WHERE (UUID=@uuid) AND (Token=@token)", conn);
                    cmd.Parameters.AddWithValue("@uuid", user_uuid);
                    cmd.Parameters.AddWithValue("@user", data);
                    cmd.Parameters.AddWithValue("@token", this.GetToken());
                    cmd.ExecuteNonQuery();
                }
                else if (updateType == UpdateType.Password)
                {
                    dynamic u = GetUser(user_uuid).Value;
                    using MySqlCommand cmd = new($"UPDATE Users SET Password=@pass,Token=@newToken WHERE (UUID=@uuid) AND (Token=@token)", conn);
                    cmd.Parameters.AddWithValue("@uuid", user_uuid);
                    cmd.Parameters.AddWithValue("@pass", GetHashString(data));
                    cmd.Parameters.AddWithValue("@token", this.GetToken());
                    cmd.Parameters.AddWithValue("@newToken", GetHashString(user_uuid + u.Email + data + u.Username + DateTime.Now.ToString()));
                    cmd.ExecuteNonQuery();
                }
                else if (updateType == UpdateType.Email)
                {
                    using MySqlCommand cmd = new($"UPDATE Users SET Email=@email WHERE (UUID=@uuid) AND (Token=@token)", conn);
                    cmd.Parameters.AddWithValue("@uuid", user_uuid);
                    cmd.Parameters.AddWithValue("@email", data);
                    cmd.Parameters.AddWithValue("@token", this.GetToken());
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    return StatusCode(500);
                }
            }

            return StatusCode(200);
        }

        [HttpPost("/Register")]
        public ActionResult<object> RegisterUser(CreateUserInfo info)
        {
            string UUID = Guid.NewGuid().ToString("N");
            string token = GetHashString(UUID + info.Email + info.Password + info.Username + DateTime.Now.ToString());
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                using MySqlCommand dis = new($"SELECT `GetRandomDiscriminator`(@user) AS `GetRandomDiscriminator`", conn);
                dis.Parameters.AddWithValue("@user", info.Username);
                MySqlDataReader reader = dis.ExecuteReader();
                string disc = null;
                while (reader.Read()) disc = reader["GetRandomDiscriminator"].ToString();
                reader.Close();

                using MySqlCommand checkForEmail = new($"SELECT * From Users WHERE (Email=@email)", conn);
                checkForEmail.Parameters.AddWithValue("@email", info.Email);
                MySqlDataReader read = checkForEmail.ExecuteReader();
                if (read.HasRows) return StatusCode(200, new { Status = 1, Message = "Duplicate Email" });

                conn.Close();
                conn.Open();

                using MySqlCommand cmd = new($"INSERT INTO Users (UUID, Username, Discriminator, Password, Email, Token, Avatar) VALUES (@uuid, @user, @disc, @pass, @email, @tok, @avatar)", conn);
                cmd.Parameters.AddWithValue("@uuid", UUID);
                cmd.Parameters.AddWithValue("@user", info.Username);
                cmd.Parameters.AddWithValue("@disc", disc);
                cmd.Parameters.AddWithValue("@pass", GetHashString(info.Password));
                cmd.Parameters.AddWithValue("@email", info.Email);
                cmd.Parameters.AddWithValue("@tok", token);
                cmd.Parameters.AddWithValue("@avatar", Path.GetFileName(MediaController.DefaultAvatars[MediaController.GetRandom.Next(0, MediaController.DefaultAvatars.Length - 1)]));
                cmd.ExecuteNonQuery();

                using MySqlCommand createTable = new($"CREATE TABLE `{UUID}` (Id INT NOT NULL AUTO_INCREMENT, Property CHAR(255) NOT NULL, Value VARCHAR(1000) NOT NULL, PRIMARY KEY(`Id`)) ENGINE = InnoDB;", conn);
                createTable.ExecuteNonQuery();
            }
            HttpContext.Request.Headers.Add("Authorization", token);
            return GetUser(UUID);
        }

        [HttpDelete("{user_uuid}")]
        [TokenAuthorization]
        public ActionResult DeleteUser(string user_uuid)
        {
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                using MySqlCommand removeUser = new($"DELETE FROM Users WHERE (UUID=@uuid) AND (Token=@token)", conn);
                removeUser.Parameters.AddWithValue("@uuid", user_uuid);
                removeUser.Parameters.AddWithValue("@token", this.GetToken());
                if (removeUser.ExecuteNonQuery() == 0) return StatusCode(404);

                using MySqlCommand removeUserAccess = new($"DROP TABLE `{user_uuid}`", conn);
                removeUserAccess.ExecuteNonQuery();
            }
            return StatusCode(200);
        }

        public static byte[] GetHash(string inputString)
        {
            using HashAlgorithm algorithm = SHA256.Create();
            return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
        }

        public static string GetHashString(string inputString)
        {
            StringBuilder sb = new();
            foreach (byte b in GetHash(inputString))
                sb.Append(b.ToString("X2"));
            return sb.ToString();
        }

        // User info
        [HttpGet("Channels")]
        [TokenAuthorization]
        public ActionResult<List<string>> GetUserChannels()
        {
            List<string> channels = new();
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                using MySqlCommand cmd = new($"SELECT * FROM `{Context.GetUserUUID(this.GetToken())}` WHERE (Property=@prop)", conn);
                cmd.Parameters.AddWithValue("@prop", "ChannelAccess");
                //cmd.Parameters.AddWithValue("@token", this.GetToken());
                MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    channels.Add((string)reader["Value"]);
                }
            }
            return channels;
        }

        [HttpPost("Channels/Register")]
        [TokenAuthorization]
        public ActionResult AddUserChannel(string user_uuid, string channel_uuid)
        {
            if (!CheckChannelOwner(Context.GetUserUUID(this.GetToken()), channel_uuid)) return StatusCode(403);
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                using MySqlCommand addAccessToUser = new($"INSERT INTO `{user_uuid}` (Property, Value) VALUES (@prop, @val)", conn);
                addAccessToUser.Parameters.AddWithValue("@prop", "ChannelAccess");
                addAccessToUser.Parameters.AddWithValue("@val", channel_uuid);
                addAccessToUser.ExecuteNonQuery();
            }
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                using MySqlCommand addUserToChannel = new($"INSERT INTO `access_{channel_uuid}` (User_UUID) VALUES (@user)", conn);
                addUserToChannel.Parameters.AddWithValue("@user", user_uuid);
                addUserToChannel.ExecuteNonQuery();
            }
            return StatusCode(200);
        }

        [HttpDelete("Channels/Unregister")]
        [TokenAuthorization]
        public ActionResult RemoveUserChannel(string user_uuid, string channel_uuid)
        {
            if (!CheckChannelOwner(Context.GetUserUUID(this.GetToken()), channel_uuid) && Context.GetUserUUID(this.GetToken()) != user_uuid) return StatusCode(403);
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                using MySqlCommand addAccessToUser = new($"DELETE FROM `{user_uuid}` WHERE (Property=@prop) and (Value=@val)", conn);
                addAccessToUser.Parameters.AddWithValue("@prop", "ChannelAccess");
                addAccessToUser.Parameters.AddWithValue("@val", channel_uuid);
                addAccessToUser.ExecuteNonQuery();
            }
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                using MySqlCommand addUserToChannel = new($"DELETE FROM `access_{channel_uuid}` WHERE (User_UUID=@user)", conn);
                addUserToChannel.Parameters.AddWithValue("@user", user_uuid);
                addUserToChannel.ExecuteNonQuery();
            }
            return StatusCode(200);
        }

        bool CheckChannelOwner(string userUUID, string channel_uuid)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand cmd = new($"SELECT Owner_UUID FROM Channels WHERE (Table_ID=@table) AND (Owner_UUID=@user)", conn);
            cmd.Parameters.AddWithValue("@table", channel_uuid);
            cmd.Parameters.AddWithValue("@user", userUUID);
            MySqlDataReader reader = cmd.ExecuteReader();
            if (reader.HasRows) return true;
            return false;
        }
    }
}
