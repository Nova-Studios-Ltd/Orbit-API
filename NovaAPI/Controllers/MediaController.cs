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
using MimeTypes;

namespace NovaAPI.Controllers
{
    [Route("Media")]
    [ApiController]
    public class MediaController : ControllerBase
    {
        readonly NovaChatDatabaseContext Context;

        // For la dumb endpoint
        public static string[] DefaultAvatars = System.IO.Directory.GetFiles(Globals.DefaultAvatarMedia, "*.*");
        public static Random GetRandom = new();

        public MediaController(NovaChatDatabaseContext context)
        {
            Context = context;
        }

        // User related
        [HttpGet("Avatar/{user_uuid}")]
        public ActionResult GetAvatar(string user_uuid, int size = -1, bool keepAspect = false)
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
                    ResizeImage(Image.FromFile(path), size, size, keepAspect).Save(ms, ImageFormat.Png);
                    Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    return File(ms.ToArray(), "image/png");
                }
            }
            return StatusCode(500);
        }

        [HttpHead("Avatar/{user_uuid}")]
        public ActionResult HeadAvatar(string user_uuid, int size = -1, bool keepAspect = false)
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
                    ResizeImage(Image.FromFile(path), size, size, keepAspect).Save(ms, ImageFormat.Png);
                    Response.Headers.Add("Access-Control-Allow-Origin", "*");
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

        // Channel (Group) related 
        [HttpGet("Channel/{channel_uuid}/Icon")]
        public ActionResult GetChannelAvatar(string channel_uuid, int size = -1, bool keepAspect = false)
        {
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                MySqlCommand cmd = new($"SELECT ChannelIcon FROM Channels WHERE (Table_ID=@uuid)", conn);
                cmd.Parameters.AddWithValue("@uuid", channel_uuid);
                using MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (reader["ChannelIcon"] == null) return StatusCode(404);
                    string basePath = "Media/channelIcons";
                    if (((string)reader["ChannelIcon"]).Contains("default")) basePath = "Media/defaultAvatars";
                    string path = Path.Combine(basePath, (string)reader["ChannelIcon"]);
                    if (!System.IO.File.Exists(path)) return StatusCode(404);
                    MemoryStream ms = new();
                    size = size == -1 ? int.MaxValue : size;
                    ResizeImage(Image.FromFile(path), size, size, keepAspect).Save(ms, ImageFormat.Png);
                    Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    return File(ms.ToArray(), "image/png");
                }
            }
            return StatusCode(500);
        }

        [HttpHead("Channel/{channel_uuid}/Icon")]
        public ActionResult HeadChannelAvatar(string channel_uuid, int size = -1, bool keepAspect = false)
        {
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                MySqlCommand cmd = new($"SELECT Avatar FROM Users WHERE (Table_ID=@uuid)", conn);
                cmd.Parameters.AddWithValue("@uuid", channel_uuid);
                using MySqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (reader["ChannelIcon"] == null) return StatusCode(404);
                    string basePath = "Media/channelIcons";
                    if (((string)reader["ChannelIcon"]).Contains("default")) basePath = "Media/defaultAvatars";
                    string path = Path.Combine(basePath, (string)reader["ChannelIcon"]);
                    if (!System.IO.File.Exists(path)) return StatusCode(404);
                    MemoryStream ms = new();
                    size = size == -1 ? int.MaxValue : size;
                    ResizeImage(Image.FromFile(path), size, size, keepAspect).Save(ms, ImageFormat.Png);
                    Response.ContentLength = ms.Length;
                    Response.ContentType = "image/png";
                    Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    return StatusCode(200);
                }
            }
            return StatusCode(500);
        }

        [HttpPost("Channel/{channel_uuid}/Icon")]
        [TokenAuthorization]
        public ActionResult SetChannelAvatar(string channel_uuid, IFormFile file) {
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();
                MySqlCommand getAvatar = new($"SELECT ChannelIcon FROM Channels WHERE (Table_ID=@channel_uuid) AND (Owner_UUID=@owner_uuid)", conn);
                getAvatar.Parameters.AddWithValue("@channel_uuid", channel_uuid);
                getAvatar.Parameters.AddWithValue("@owner_uuid", Context.GetUserUUID(this.GetToken()));
                using MySqlDataReader reader = getAvatar.ExecuteReader();
                while (reader.Read())
                {
                    string basePath = "Media/channelIcons";
                    if (((string)reader["ChannelIcon"]).Contains("default")) basePath = "Media/defaultAvatars";
                    string oldAvatar = Path.Combine(basePath, (string)reader["ChannelIcon"]);
                    if (!Regex.IsMatch((string)reader["ChannelIcon"], "defaultAvatar*") && System.IO.File.Exists(oldAvatar))
                        System.IO.File.Delete(oldAvatar);
                    string newAvatar = CreateMD5(file.FileName + DateTime.Now.ToString());
                    string newAvatarPath = Path.Combine("Media/channelIcons", newAvatar);
                    FileStream fs = System.IO.File.OpenWrite(newAvatarPath);
                    file.CopyTo(fs);
                    fs.Close();
                    conn.Close();
                    conn.Open();
                    MySqlCommand setAvatar = new($"UPDATE Channels SET ChannelIcon=@avatar WHERE (Table_ID=@channel_uuid) AND (Owner_UUID=@owner_uuid)", conn);
                    setAvatar.Parameters.AddWithValue("@channel_uuid", channel_uuid);
                    setAvatar.Parameters.AddWithValue("@avatar", newAvatar);
                    setAvatar.Parameters.AddWithValue("@owner_uuid", Context.GetUserUUID(this.GetToken()));
                    setAvatar.ExecuteNonQuery();
                    return StatusCode(200);
                }
            }
            return StatusCode(404);
        }

        [HttpGet("Channel/{channel_uuid}/{content_id}")]
        public ActionResult GetContent(string channel_uuid, string content_id)
        {
            string path = Path.Combine(Globals.ChannelMedia, channel_uuid, content_id);
            if (!System.IO.File.Exists(path)) return StatusCode(404);
            FileStream fs = System.IO.File.OpenRead(path);
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            return File(fs, RetreiveMimeType(content_id));
        }

        // Content Related
        [HttpPost("Channel/{channel_uuid}")]
        [TokenAuthorization]
        public ActionResult<string> PostContent(string channel_uuid, IFormFile file)
        {
            string user_uuid = Context.GetUserUUID(this.GetToken());
            if (!ChannelUtils.CheckUserChannelAccess(Context, user_uuid, channel_uuid)) return StatusCode(403);
            string c = Path.Combine(Globals.ChannelMedia, channel_uuid);
            if (!Directory.Exists(Path.Combine(Globals.ChannelMedia, channel_uuid))) return StatusCode(404);
            if (file.Length >= 20971520) return StatusCode(413);
            if (!Globals.ContentTypes.Any(x => file.FileName.Contains(x))) return StatusCode(415);
            string filename = Guid.NewGuid().ToString();
            string fileLoc = Path.Combine(Globals.ChannelMedia, channel_uuid, filename);

            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand cmd = new($"INSERT INTO ChannelMedia (File_UUID, Filename, MimeType, Size) VALUES (@uuid, @filename, @mime, @size)", conn);
            cmd.Parameters.AddWithValue("@uuid", filename);
            cmd.Parameters.AddWithValue("@filename", file.FileName);
            cmd.Parameters.AddWithValue("@mime", MimeTypeMap.GetMimeType(Path.GetExtension(file.FileName)));
            cmd.Parameters.AddWithValue("@size", file.Length);
            if (cmd.ExecuteNonQuery() == 0) return StatusCode(500);

            FileStream fs = System.IO.File.OpenWrite(fileLoc);
            file.CopyTo(fs);
            fs.Close();
            return filename;
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
        private Image ResizeImage(Image img, int maxWidth, int maxHeight, bool keepAspect)
        {
            if (img.Height < maxHeight && img.Width < maxWidth) return img;
            using (img)
            {
                Double xRatio = (double)img.Width / maxWidth;
                Double yRatio = (double)img.Height / maxHeight;
                Double ratio = Math.Max(xRatio, yRatio);
                int nnx = (keepAspect)? (int)Math.Floor(img.Width / ratio) : maxWidth;
                int nny = (keepAspect)? (int)Math.Floor(img.Height / ratio): maxHeight;
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

        public string RetreiveMimeType(string content_id)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand cmd = new("SELECT MimeType FROM ChannelMedia WHERE (File_UUID=@uuid)", conn);
            cmd.Parameters.AddWithValue("@uuid", content_id);
            MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                return reader["MimeType"].ToString();
            }
            return "";
        }
    }
}
