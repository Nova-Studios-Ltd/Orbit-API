using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using NovaAPI.Attri;
using NovaAPI.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NovaAPI.Controllers
{
    [Route("Media")]
    [ApiController]
    public class MediaController : ControllerBase
    {
        NovaChatDatabaseContext Context;
        public MediaController(NovaChatDatabaseContext context)
        {
            Context = context;
        }

        [HttpGet("Avatar/{user_uuid}")]
        public ActionResult GetAvatar(string user_uuid)
        {
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                MySqlCommand cmd = new($"SELECT Avatar FROM Users WHERE (UUID=@uuid)", conn);
                cmd.Parameters.AddWithValue("@uuid", user_uuid);
                using MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string path = Path.Combine("Media/avatars", (string)reader["Avatar"]);
                    if (!System.IO.File.Exists(path)) return StatusCode(404);
                    return File(System.IO.File.OpenRead(path), "image/png");
                }
            }
            return StatusCode(500);
        }

        [HttpPost("Avatar/{user_uuid}")]
        [TokenAuthorization]
        public ActionResult SetAvatar(string user_uuid, IFormFile file)
        {
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                MySqlCommand getAvatar = new($"SELECT Avatar FROM Users WHERE (UUID=@uuid) AND (Token=@token)", conn);
                getAvatar.Parameters.AddWithValue("@uuid", user_uuid);
                getAvatar.Parameters.AddWithValue("@token", this.GetToken());
                using MySqlDataReader reader = getAvatar.ExecuteReader();
                while (reader.Read())
                {
                    string oldAvatar = Path.Combine("Media/avatars", (string)reader["Avatar"]);
                    if (!Regex.IsMatch((string)reader["Avatar"], "defaultAvatar*") && System.IO.File.Exists(oldAvatar))
                        System.IO.File.Delete(oldAvatar);
                    string newAvatar = Path.Combine("Media/avatars", CreateMD5(file.FileName + DateTime.Now.ToString()));
                    FileStream fs = System.IO.File.OpenWrite(newAvatar);
                    file.CopyTo(fs);
                    fs.Close();
                    conn.Close();
                    conn.Open();
                    MySqlCommand setAvatar = new($"UPDATE Users SET Avatar=@avatar WHERE (UUID=@uuid)", conn);
                    setAvatar.Parameters.AddWithValue("@uuid", user_uuid);
                    setAvatar.Parameters.AddWithValue("@avatar", newAvatar);
                    setAvatar.ExecuteNonQuery();
                    return StatusCode(200);
                }
            }
            return StatusCode(404);
        }

        public static string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }
    }
}
