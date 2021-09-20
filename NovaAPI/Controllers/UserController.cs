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

namespace NovaAPI.Controllers
{
    [Route("Users")]
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
                MySqlCommand cmd = new($"SELECT * FROM Users WHERE (UUID=@uuid) AND (Token=@token)", conn);
                cmd.Parameters.AddWithValue("@uuid", user_uuid);
                cmd.Parameters.AddWithValue("@token", GetToken());
                using MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    user = new
                    {
                        UUID = reader["UUID"].ToString(),
                        Username = reader["Username"].ToString(),
                        Email = reader["Email"].ToString(),
                        CreationDate = DateTime.Parse(reader["CreationDate"].ToString())
                    };
                }
            }

            if (user == null)
            {
                return StatusCode(404);
            }

            return user;
        }

        [HttpGet("/Login")]
        public ActionResult<object> LoginUser(string username, string password)
        {
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                MySqlCommand cmd = new($"SELECT * FROM Users WHERE (Username=@user) AND (Password=@pass)", conn);
                cmd.Parameters.AddWithValue("@user", username);
                cmd.Parameters.AddWithValue("@pass", GetHashString(password));
                using MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    return new
                    {
                        UUID = reader["UUID"].ToString(),
                        Token = reader["Token"].ToString()
                    };
                }
            }
            return StatusCode(404);
        }

        //TODO Fix this stupid function
        [HttpPut("{user_uuid}")]
        [TokenAuthorization]
        public ActionResult UpdateUser(string user_uuid, UpdateType updateType, [FromBody] object json)
        {
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                if (updateType == UpdateType.Username)
                {
                    using MySqlCommand cmd = new($"UPDATE Users SET Username=@user WHERE UUID=@uuid", conn);
                    cmd.Parameters.AddWithValue("@uuid", user_uuid);
                    cmd.Parameters.AddWithValue("@user", data);
                    cmd.ExecuteNonQuery();
                }
                else if (updateType == UpdateType.Password)
                {
                    dynamic u = GetUser(user_uuid);
                    using MySqlCommand cmd = new($"UPDATE Users SET Password=@pass,Token=@token WHERE UUID=@uuid", conn);
                    cmd.Parameters.AddWithValue("@uuid", user_uuid);
                    cmd.Parameters.AddWithValue("@pass", GetHashString(data));
                    cmd.Parameters.AddWithValue("@token", GetHashString(user_uuid + u.Email + data + u.Username + DateTime.Now.ToString()));
                    cmd.ExecuteNonQuery();
                }
                else if (updateType == UpdateType.Email)
                {
                    using MySqlCommand cmd = new($"UPDATE Users SET Email=@email WHERE UUID=@uuid", conn);
                    cmd.Parameters.AddWithValue("@uuid", user_uuid);
                    cmd.Parameters.AddWithValue("@email", data);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    return NotFound();
                }
            }

            return NoContent();
        }

        //[HttpPut("/{uuid}")]
        //[TokenAuthorization]
        //public ActionResult AddChannel(string uuid, string channel_uuid)
        //{
        //    using (MySqlConnection conn = Context.GetUsers())
        //    {
        //        conn.Open();

        //        using MySqlCommand cmd = new($"INSERT INTO `{uuid}` (Property, Value) VALUES (@property, @uuid)", conn);
        //        cmd.Parameters.AddWithValue("@property", "ChannelAccess");
        //        cmd.Parameters.AddWithValue("@uuid", channel_uuid);
        //        cmd.ExecuteNonQuery();
        //    }
        //    return NoContent();
        //}

        [HttpPost("/Register")]
        public ActionResult<User> RegisterUser(LoginInfo loginInfo)
        {
            string UUID = Guid.NewGuid().ToString("N");
            string token = GetHashString(UUID + loginInfo.Email + loginInfo.Password + loginInfo.Username + DateTime.Now.ToString());
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();

                using MySqlCommand cmd = new($"INSERT INTO Users (UUID, Username, Password, Email, Token) VALUES (@uuid, @user, @pass, @email, @tok)", conn);
                cmd.Parameters.AddWithValue("@uuid", UUID);
                cmd.Parameters.AddWithValue("@user", loginInfo.Username);
                cmd.Parameters.AddWithValue("@pass", GetHashString(loginInfo.Password));
                cmd.Parameters.AddWithValue("@email", loginInfo.Email);
                cmd.Parameters.AddWithValue("@tok", token);
                cmd.ExecuteNonQuery();

                using MySqlCommand createTable = new($"CREATE TABLE `{UUID}` (Id INT NOT NULL AUTO_INCREMENT, Property CHAR(255) NOT NULL, Value VARCHAR(1000) NOT NULL, PRIMARY KEY(`Id`)) ENGINE = InnoDB;", conn);
                createTable.ExecuteNonQuery();
            }

            return CreatedAtAction("GetUser", new { uuid = UUID }, GetUser(UUID).Value);
        }

        [HttpDelete("{user_uuid}")]
        [TokenAuthorization]
        public ActionResult DeleteUser(string user_uuid)
        {
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                using MySqlCommand cmd = new($"DELETE FROM Users WHERE (UUID=@uuid) AND (Token=@token)", conn);
                cmd.Parameters.AddWithValue("@uuid", user_uuid);
                cmd.Parameters.AddWithValue("@token", GetToken());
                if (cmd.ExecuteNonQuery() == 0) return StatusCode(404);
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

        string GetToken()
        {
            if (!Request.Headers.TryGetValue("Authorization", out StringValues values))
                return "";
            return values.First();
        }
    }
}
