using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public ActionResult<User> RetUser(string user_uuid)
        {
            User user = null;
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                MySqlCommand cmd = new($"SELECT * FROM Users WHERE (UUID=@uuid)", conn);
                cmd.Parameters.AddWithValue("@uuid", user_uuid);
                using MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    user = new User()
                    {
                        UUID = reader["UUID"].ToString(),
                        Username = reader["Username"].ToString(),
                        Discriminator = reader["Discriminator"].ToString().PadLeft(4, '0'),
                        CreationDate = DateTime.Parse(reader["CreationDate"].ToString()),
                        Avatar = $"https://api.novastudios.tk/Media/Avatar/{(reader["UUID"].ToString())}?size=64"
                    };
                }
            }

            if (user == null)
            {
                return StatusCode(404);
            }

            return user;
        }

        [HttpGet("@me")]
        [TokenAuthorization]
        public ActionResult<User> GetSelf()
        {
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();
            using MySqlCommand cmd = new($"SELECT * FROM Users WHERE (UUID=@uuid)", conn);
            cmd.Parameters.AddWithValue("@uuid", Context.GetUserUUID(this.GetToken()));
            using MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                return new User()
                {
                    UUID = reader["UUID"].ToString(),
                    Username = reader["Username"].ToString(),
                    Email = reader["Email"].ToString(),
                    Discriminator = reader["Discriminator"].ToString().PadLeft(4, '0'),
                    CreationDate = DateTime.Parse(reader["CreationDate"].ToString()),
                    Avatar = $"https://api.novastudios.tk/Media/Avatar/{(reader["UUID"].ToString())}?size=64"
                };
            }
            
            return StatusCode(404, "Unable to find yourself");
        }
        
        [HttpPatch("{user_uuid}/Username")]
        [Obsolete("Please use User/@me/Username")]
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
        
        [HttpPatch("@me/Username")]
        [TokenAuthorization]
        public ActionResult ChangeUsername([FromBody] string username)
        {
            string user_uuid = Context.GetUserUUID(this.GetToken());
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
        [Obsolete("Please use User/@me/Password")]
        [TokenAuthorization]
        public ActionResult ChangePassword(string user_uuid, PasswordUpdate update)
        {
            if (string.IsNullOrEmpty(update.Password)) return StatusCode(400);
            if (!Context.UserExsists(user_uuid)) return StatusCode(404);
            dynamic u = RetUser(user_uuid).Value;
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

        [HttpPatch("@me/Password")]
        [TokenAuthorization]
        public ActionResult ChangePassword(PasswordUpdate update)
        {
            string user_uuid = Context.GetUserUUID(this.GetToken());
            if (string.IsNullOrEmpty(update.Password)) return StatusCode(400);
            if (!Context.UserExsists(user_uuid)) return StatusCode(404);
            dynamic u = RetUser(user_uuid).Value;
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
        [Obsolete("Please use User/@me/Email")]
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

        [HttpPatch("@me/Email")]
        [TokenAuthorization]
        public ActionResult ChangeEmail([FromBody] string email)
        {
            string user_uuid = Context.GetUserUUID(this.GetToken());
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
        [Obsolete("Please use /Auth/Login instead")]
        public ActionResult<ReturnLoginUserInfo> LoginUser(LoginUserInfo info)
        {
            return StatusCode(410, "Please use /Auth/Login");
        }

        [HttpPost("/Register")]
        [Obsolete("Please use /Auth/Register instead")]
        public ActionResult<User> RegisterUser(CreateUserInfo info)
        {
            return StatusCode(410, "Please use /Auth/Register");
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
        [Obsolete("Please use User/@me")]
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

        [HttpDelete("@me")]
        [TokenAuthorization]
        public ActionResult DeleteUser()
        {
            string user_uuid = Context.GetUserUUID(this.GetToken());
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
        [Obsolete("Please use User/@me/Keystore")]
        [TokenAuthorization]
        public ActionResult<Dictionary<string, string>> GetKeystore(string user_uuid)
        {
            Response.Redirect($"/User/@me/Keystore");
            return StatusCode(302);
        }

        [HttpGet("{user_uuid}/Keystore/{key_user_uuid}")]
        [Obsolete("Please use User/@me/Keystore/{key_user_uuid}")]
        [TokenAuthorization]
        public ActionResult<string> GetKey(string user_uuid, string key_user_uuid)
        {
            Response.Redirect($"/User/@me/{key_user_uuid}");
            return StatusCode(302);
        }


        [HttpPost("{user_uuid}/Keystore/{key_user_uuid}")]
        [Obsolete("Please user User/@me/Keystore/{key_user_uuid}")]
        [TokenAuthorization]
        public ActionResult SetKey(string user_uuid, string key_user_uuid, [FromBody] string key)
        {
            Response.Redirect($"/User/@me/{key_user_uuid}");
            Response.Body.Write(Encoding.UTF8.GetBytes(key));
            return StatusCode(302);
        }


        // TODO Remove Channels
        [HttpGet("Channels")]
        [HttpGet("@me/Channels")]
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
    }
}
