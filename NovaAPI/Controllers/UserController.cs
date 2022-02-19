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
        private static readonly RNGCryptoServiceProvider rngCsp = new();
        private readonly NovaChatDatabaseContext Context;
        private readonly EventManager Event;

        public UserController(NovaChatDatabaseContext context, EventManager e)
        {
            Context = context;
            Event = e;
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
                            Discriminator = reader["Discriminator"].ToString().PadLeft(4, '0'),
                            Email = reader["Email"].ToString(),
                            CreationDate = DateTime.Parse(reader["CreationDate"].ToString()),
                            Avatar = $"https://api.novastudios.tk/Media/Avatar/{(reader["UUID"].ToString())}?size=64"
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
                            Discriminator = reader["Discriminator"].ToString().PadLeft(4, '0'),
                            CreationDate = DateTime.Parse(reader["CreationDate"].ToString()),
                            Avatar = $"https://api.novastudios.tk/Media/Avatar/{(reader["UUID"].ToString())}?size=64"
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

        [HttpPatch("{user_uuid}/Username")]
        [TokenAuthorization]
        public ActionResult ChangeUsername(string user_uuid, [FromBody] string username)
        {
            if (string.IsNullOrEmpty(username)) return StatusCode(400);
            if (!Context.UserExsists(user_uuid)) return StatusCode(404);
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();
            using MySqlCommand cmd = new($"UPDATE Users SET Username=@user WHERE (UUID=@uuid) AND (Token=@token)", conn);
            cmd.Parameters.AddWithValue("@uuid", user_uuid);
            cmd.Parameters.AddWithValue("@user", username);
            cmd.Parameters.AddWithValue("@token", this.GetToken());
            cmd.ExecuteNonQuery();
            return StatusCode(200);
        }

        [HttpPatch("{user_uuid}/Password")]
        [TokenAuthorization]
        public ActionResult ChangePassword(string user_uuid, PasswordUpdate update)
        {
            if (string.IsNullOrEmpty(update.Password)) return StatusCode(400);
            if (!Context.UserExsists(user_uuid)) return StatusCode(404);
            dynamic u = GetUser(user_uuid).Value;
            byte[] salt = EncryptionUtils.GetSalt(64);
            using MySqlConnection conn = Context.GetUsers();
            using MySqlCommand cmd = new($"UPDATE Users SET Password=@pass,Salt=@salt,Token=@newToken,PrivKey=@key,IV=@iv WHERE (UUID=@uuid) AND (Token=@token)", conn);
            cmd.Parameters.AddWithValue("@uuid", user_uuid);
            cmd.Parameters.AddWithValue("@pass", EncryptionUtils.GetSaltedHashString(update.Password, salt));
            cmd.Parameters.AddWithValue("@salt", salt);
            cmd.Parameters.AddWithValue("@token", this.GetToken());
            cmd.Parameters.AddWithValue("@newToken", EncryptionUtils.GetSaltedHashString(user_uuid + u.Email + update.Password + u.Username + DateTime.Now.ToString(), EncryptionUtils.GetSalt(8)));
            cmd.Parameters.AddWithValue("@privKey", update.Key.Content);
            cmd.Parameters.AddWithValue("@iv", update.Key.IV);
            cmd.ExecuteNonQuery();
            return StatusCode(200);
        }

        [HttpPatch("{user_uuid}/Email")]
        [TokenAuthorization]
        public ActionResult ChangeEmail(string user_uuid, [FromBody] string email)
        {
            if (string.IsNullOrEmpty(email)) return StatusCode(400);
            if (!Context.UserExsists(user_uuid)) return StatusCode(404);
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();
            using MySqlCommand cmd = new($"UPDATE Users SET Email=@email WHERE (UUID=@uuid) AND (Token=@token)", conn);
            cmd.Parameters.AddWithValue("@uuid", user_uuid);
            cmd.Parameters.AddWithValue("@email", email);
            cmd.Parameters.AddWithValue("@token", this.GetToken());
            cmd.ExecuteNonQuery();
            return StatusCode(200);
        }

        [HttpPost("/Login")]
        public ActionResult<ReturnLoginUserInfo> LoginUser(LoginUserInfo info)
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
                        if (reader["Password"].ToString() == EncryptionUtils.GetSaltedHashString(info.Password, (byte[])reader["Salt"]))
                            return new ReturnLoginUserInfo
                            {
                                UUID = reader["UUID"].ToString(),
                                Token = reader["Token"].ToString(),
                                PublicKey = reader["PubKey"].ToString(),
                                Key = new AESMemoryEncryptData
                                {
                                    Content = reader["PrivKey"].ToString(),
                                    IV = reader["IV"].ToString()
                                }
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

        [HttpPost("/Register")]
        public ActionResult<object> RegisterUser(CreateUserInfo info)
        {
            string UUID = Guid.NewGuid().ToString("N");
            string token = EncryptionUtils.GetSaltedHashString(UUID + info.Email + EncryptionUtils.GetHashString(info.Password) + info.Username + DateTime.Now.ToString(), EncryptionUtils.GetSalt(8));
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
                if (read.HasRows) return StatusCode(409);

                conn.Close();
                conn.Open();
                
                // Get Salt - Sometimes things are better with a bit of salt
                byte[] salt = EncryptionUtils.GetSalt(64);

                using MySqlCommand cmd = new($"INSERT INTO Users (UUID, Username, Discriminator, Password, Salt, Email, Token, Avatar, PubKey, PrivKey, IV) VALUES (@uuid, @user, @disc, @pass, @salt, @email, @tok, @avatar, @pubKey, @privKey, @iv)", conn);
                cmd.Parameters.AddWithValue("@uuid", UUID);
                cmd.Parameters.AddWithValue("@user", info.Username);
                cmd.Parameters.AddWithValue("@disc", disc);
                cmd.Parameters.AddWithValue("@pass", EncryptionUtils.GetSaltedHashString(info.Password, salt));
                cmd.Parameters.AddWithValue("@salt", salt);
                cmd.Parameters.AddWithValue("@email", info.Email);
                cmd.Parameters.AddWithValue("@tok", token);
                cmd.Parameters.AddWithValue("@avatar", Path.GetFileName(MediaController.DefaultAvatars[MediaController.GetRandom.Next(0, MediaController.DefaultAvatars.Length - 1)]));
                cmd.Parameters.AddWithValue("@pubKey", info.Key.Pub);
                cmd.Parameters.AddWithValue("@privKey", info.Key.Priv);
                cmd.Parameters.AddWithValue("@iv", info.Key.PrivIV);
                cmd.ExecuteNonQuery();

                using MySqlCommand createTable = new($"CREATE TABLE `{UUID}` (Id INT NOT NULL AUTO_INCREMENT, Property CHAR(255) NOT NULL, Value VARCHAR(1000) NOT NULL, PRIMARY KEY(`Id`)) ENGINE = InnoDB;", conn);
                createTable.ExecuteNonQuery();

                using MySqlCommand createKeystore = new($"CREATE TABLE `{UUID}_keystore` (UUID CHAR(255) NOT NULL , PubKey VARCHAR(1000) NOT NULL , PRIMARY KEY (`UUID`)) ENGINE = InnoDB;", conn);
                createKeystore.ExecuteNonQuery();

                using MySqlCommand createFriends = new($"CREATE TABLE `{UUID}_friends` (Id INT NOT NULL AUTO_INCREMENT, UUID CHAR(255) NOT NULL, State CHAR(255) NOT NULL, PRIMARY KEY (`Id`), UNIQUE (`UUID`)) ENGINE = InnoDB;", conn);
                createFriends.ExecuteNonQuery();
            }
            HttpContext.Request.Headers.Add("Authorization", token);
            return GetUser(UUID);
        }
        
        [HttpGet("{username}/{discriminator}/UUID")]
        public ActionResult<string> GetUserUUIDFromUsername(string username, string discriminator)
        {
            if (int.TryParse(discriminator, out int disc)) 
            {
                using (MySqlConnection conn = Context.GetUsers()) 
                {
                    conn.Open();
                    using MySqlCommand getUser = new($"Select UUID FROM Users WHERE (Username=@username) AND (Discriminator=@disc)", conn);
                    getUser.Parameters.AddWithValue("@username", username);
                    getUser.Parameters.AddWithValue("@disc", disc);
                    MySqlDataReader reader = getUser.ExecuteReader();
                    while (reader.Read()) 
                    {
                        return reader["UUID"].ToString();
                    }
                }
            }
            return StatusCode(404, $"Unable to find user {username}#{discriminator}");
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

                using MySqlConnection keystoreUser = Context.GetUsers();
                keystoreUser.Open();
                using MySqlCommand keystore = new($"SELECT * FROM `{user_uuid}_keystore`", keystoreUser);
                MySqlDataReader reader = keystore.ExecuteReader();
                while (reader.Read())
                {
                    using MySqlCommand cmd = new($"DELETE FROM `{reader["UUID"].ToString()}_keystore` WHERE (UUID=@uuid)", conn);
                    cmd.Parameters.AddWithValue("@uuid", user_uuid);
                    Event.KeyAddedToKeystore(reader["UUID"].ToString(), user_uuid);
                    cmd.ExecuteNonQuery();
                }
                keystoreUser.Close();

                using MySqlCommand removeUserAccess = new($"DROP TABLE `{user_uuid}`, `{user_uuid}_keystore`", conn);
                removeUserAccess.ExecuteNonQuery();
            }
            return StatusCode(200);
        }

        [HttpGet("{user_uuid}/Keystore")]
        [TokenAuthorization]
        public ActionResult<Dictionary<string, string>> GetKeystore(string user_uuid)
        {
            if (Context.GetUserUUID(this.GetToken()) != user_uuid) return StatusCode(403);
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();
            using MySqlCommand cmd = new($"SELECT * FROM `{user_uuid}_keystore`", conn);
            MySqlDataReader reader = cmd.ExecuteReader();
            Dictionary<string, string> uuidKey = new();
            while (reader.Read())
            {
                uuidKey.Add(reader["UUID"].ToString(), reader["PubKey"].ToString());
            }
            if (uuidKey.Count == 0) return StatusCode(404);
            return uuidKey;
        }

        [HttpGet("{user_uuid}/Keystore/{key_user_uuid}")]
        [TokenAuthorization]
        public ActionResult<string> GetKey(string user_uuid, string key_user_uuid)
        {
            if (Context.GetUserUUID(this.GetToken()) != user_uuid) return StatusCode(403);
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();
            using MySqlCommand cmd = new($"SELECT * FROM `{user_uuid}_keystore`", conn);
            MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader["UUID"].ToString() == key_user_uuid)
                    return reader["PubKey"].ToString();
            }
            return StatusCode(404);
        }

        [HttpPost("{user_uuid}/Keystore/{key_user_uuid}")]
        [TokenAuthorization]
        public ActionResult SetKey(string user_uuid, string key_user_uuid, [FromBody] string key)
        {
            if (Context.GetUserUUID(this.GetToken()) != user_uuid) return StatusCode(403);
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();
            using MySqlCommand cmd = new($"INSERT INTO `{user_uuid}_keystore` (UUID, PubKey) VALUES (@uuid, @key)", conn);
            cmd.Parameters.AddWithValue("@uuid", key_user_uuid);
            cmd.Parameters.AddWithValue("@key", key);
            if (cmd.ExecuteNonQuery() == 0) return StatusCode(409);
            return StatusCode(200);
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
                cmd.Parameters.AddWithValue("@prop", "ActiveChannelAccess");
                //cmd.Parameters.AddWithValue("@token", this.GetToken());
                MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    channels.Add((string)reader["Value"]);
                }
            }
            return channels;
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
