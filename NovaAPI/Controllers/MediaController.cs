using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using NovaAPI.Attri;
using NovaAPI.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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
        readonly NovaChatDatabaseContext Context;

        // For la dumb endpoint
        public static string[] DefaultAvatars = System.IO.Directory.GetFiles("./Media/defaultAvatars");
        public static Random GetRandom = new Random();

        public MediaController(NovaChatDatabaseContext context)
        {
            Context = context;
        }

        [HttpGet("Avatar/{user_uuid}")]
        public ActionResult GetAvatar(string user_uuid, int size=-1)
        {
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                MySqlCommand cmd = new($"SELECT Avatar FROM Users WHERE (UUID=@uuid)", conn);
                cmd.Parameters.AddWithValue("@uuid", user_uuid);
                using MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string basePath = "Media/avatars";
                    if (((string)reader["Avatar"]).Contains("default")) basePath = "Media/defaultAvatars";
                    string path = Path.Combine(basePath, (string)reader["Avatar"]);
                    if (!System.IO.File.Exists(path)) return StatusCode(404);
                    MemoryStream ms = new();
                    size = size == -1 ? int.MaxValue : size;
                    ResizeImage(Image.FromFile(path), size, size).Save(ms, ImageFormat.Png);
                    return File(ms.ToArray(), "image/png");
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
                    string basePath = "Media/avatars";
                    if (((string)reader["Avatar"]).Contains("default")) basePath = "Media/defaultAvatars";
                    string oldAvatar = Path.Combine(basePath, (string)reader["Avatar"]);
                    if (!Regex.IsMatch((string)reader["Avatar"], "defaultAvatar*") && System.IO.File.Exists(oldAvatar))
                        System.IO.File.Delete(oldAvatar);
                    string newAvatar = CreateMD5(file.FileName + DateTime.Now.ToString());
                    string newAvatarPath = Path.Combine("Media/avatars", newAvatar);
                    FileStream fs = System.IO.File.OpenWrite(newAvatarPath);
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

        [HttpGet("Avatar/{user_uuid}/Random")]
        [TokenAuthorization]
        public ActionResult SetRandomAvatar(string user_uuid)
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
                    string basePath = "Media/avatars";
                    if (((string)reader["Avatar"]).Contains("default")) basePath = "Media/defaultAvatars";
                    string oldAvatar = Path.Combine(basePath, (string)reader["Avatar"]);
                    if (!Regex.IsMatch((string)reader["Avatar"], "defaultAvatar*") && System.IO.File.Exists(oldAvatar))
                        System.IO.File.Delete(oldAvatar);
                    conn.Close();
                    conn.Open();
                    MySqlCommand setAvatar = new($"UPDATE Users SET Avatar=@avatar WHERE (UUID=@uuid)", conn);
                    setAvatar.Parameters.AddWithValue("@uuid", user_uuid);
                    setAvatar.Parameters.AddWithValue("@avatar", Path.GetFileName(DefaultAvatars[GetRandom.Next(0, DefaultAvatars.Length - 1)]));
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
        private Image ResizeImage(Image img, int maxWidth, int maxHeight)
        {
            if (img.Height < maxHeight && img.Width < maxWidth) return img;
            using (img)
            {
                Double xRatio = (double)img.Width / maxWidth;
                Double yRatio = (double)img.Height / maxHeight;
                Double ratio = Math.Max(xRatio, yRatio);
                int nnx = (int)Math.Floor(img.Width / ratio);
                int nny = (int)Math.Floor(img.Height / ratio);
                Bitmap cpy = new Bitmap(nnx, nny, PixelFormat.Format32bppArgb);
                using (Graphics gr = Graphics.FromImage(cpy))
                {
                    gr.Clear(Color.Transparent);

                    // This is said to give best quality when resizing images
                    gr.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    gr.DrawImage(img,
                        new Rectangle(0, 0, nnx, nny),
                        new Rectangle(0, 0, img.Width, img.Height),
                        GraphicsUnit.Pixel);
                }
                return cpy;
            }

        }
    }
}
