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
using System.Diagnostics;
using static NovaAPI.Util.StorageUtil;
using NovaAPI.DataTypes;

namespace NovaAPI.Controllers
{
    [ApiController]
    public class MediaController : ControllerBase
    {
        readonly NovaChatDatabaseContext Context;


        public MediaController(NovaChatDatabaseContext context)
        {
            Context = context;
        }

        // User related
        [HttpGet("/User/{user_uuid}/Avatar")]
        public ActionResult GetAvatar(string user_uuid, int size = -1, bool keepAspect = false)
        {
            MediaFile file = RetreiveFile(MediaType.Avatar, user_uuid);
            MemoryStream ms = new();
            size = size == -1 ? int.MaxValue : size;
            ResizeImage(Image.FromStream(file.File), size, size, keepAspect).Save(ms, ImageFormat.Png);
            return File(ms.ToArray(), "image/png");
        }

        [HttpHead("/User/{user_uuid}/Avatar")]
        public ActionResult HeadAvatar(string user_uuid, int size = -1, bool keepAspect = false)
        {
            MediaFile file = RetreiveFile(MediaType.Avatar, user_uuid);
            MemoryStream ms = new();
            size = size == -1 ? int.MaxValue : size;
            ResizeImage(Image.FromStream(file.File), size, size, keepAspect).Save(ms, ImageFormat.Png);
            Response.ContentLength = ms.Length;
            Response.ContentType = file.Meta.MimeType;
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            return StatusCode(200);
        }

        [HttpPost("/User/{user_uuid}/Avatar")]
        [TokenAuthorization]
        public ActionResult SetAvatar(string user_uuid, IFormFile file)
        {
            string filename = StoreFile(MediaType.Avatar, file.OpenReadStream(), new AvatarMeta(file.FileName, file.Length, user_uuid));
            return (filename == "")? StatusCode(200) : StatusCode(404);
        }

        // Channel (Group) related 
        [HttpGet("/Channel/{channel_uuid}/Icon")]
        public ActionResult GetChannelAvatar(string channel_uuid, int size = -1, bool keepAspect = false)
        {
            MediaFile file = RetreiveFile(MediaType.ChannelIcon, channel_uuid);
            if (file == null) return StatusCode(404);
            MemoryStream ms = new();
            size = size == -1 ? int.MaxValue : size;
            ResizeImage(Image.FromStream(file.File), size, size, keepAspect).Save(ms, ImageFormat.Png);
            return File(ms.ToArray(), "image/png");
        }

        [HttpHead("/Channel/{channel_uuid}/Icon")]
        public ActionResult HeadChannelAvatar(string channel_uuid, int size = -1, bool keepAspect = false)
        {
            MediaFile file = RetreiveFile(MediaType.ChannelIcon, channel_uuid);
            if (file == null) return StatusCode(404);
            MemoryStream ms = new();
            size = size == -1 ? int.MaxValue : size;
            ResizeImage(Image.FromStream(file.File), size, size, keepAspect).Save(ms, ImageFormat.Png);
            Response.ContentLength = ms.Length;
            Response.ContentType = file.Meta.MimeType;
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            return StatusCode(200);
        }

        [HttpPost("/Channel/{channel_uuid}/Icon")]
        [TokenAuthorization]
        public ActionResult SetChannelAvatar(string channel_uuid, IFormFile file) {
            StoreFile(MediaType.ChannelIcon, file.OpenReadStream(), new IconMeta(file.FileName, file.Length, channel_uuid));
            return StatusCode(200);
        }

        [HttpPost("/Channel/{channel_uuid}/ClearIcon")]
        [Obsolete("No longer used")]
        public ActionResult ClearChannelAvatar(string channel_uuid) {
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
                    conn.Close();
                    conn.Open();
                    MySqlCommand setAvatar = new($"UPDATE Channels SET ChannelIcon=@avatar WHERE (Table_ID=@channel_uuid) AND (Owner_UUID=@owner_uuid)", conn);
                    setAvatar.Parameters.AddWithValue("@channel_uuid", channel_uuid);
                    setAvatar.Parameters.AddWithValue("@avatar", "");
                    setAvatar.Parameters.AddWithValue("@owner_uuid", Context.GetUserUUID(this.GetToken()));
                    setAvatar.ExecuteNonQuery();
                    return StatusCode(200);
                }
            }
            return StatusCode(404);
        }

        [HttpGet("/Channel/{channel_uuid}/{content_id}")]
        public ActionResult GetContent(string channel_uuid, string content_id)
        {
            string path = Path.Combine(ChannelContent, channel_uuid, content_id);
            if (!System.IO.File.Exists(path)) return StatusCode(404);
            MediaFile file = RetreiveFile(MediaType.ChannelContent, content_id, channel_uuid);
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            return File(file.File, file.Meta.MimeType);
        }

        [HttpHead("Channel/{channel_uuid}/{content_id}")]
        public ActionResult HeadContent(string channel_uuid, string content_id)
        {
            string path = Path.Combine(ChannelContent, channel_uuid, content_id);
            if (!System.IO.File.Exists(path)) return StatusCode(404);
            MediaFile file = RetreiveFile(MediaType.ChannelContent, content_id, channel_uuid);
            Response.ContentLength = file.Meta.Filesize;
            Response.ContentType = file.Meta.MimeType;
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            return StatusCode(200);
        }

        // Content Related
        [HttpPost("/Channel/{channel_uuid}")]
        [TokenAuthorization]
        public ActionResult<string> PostContent(string channel_uuid, IFormFile file, int width=0, int height=0)
        {
            string user_uuid = Context.GetUserUUID(this.GetToken());
            if (!ChannelUtils.CheckUserChannelAccess(Context, user_uuid, channel_uuid)) return StatusCode(403);
            if (!Directory.Exists(Path.Combine(ChannelContent, channel_uuid))) return StatusCode(404);
            if (file.Length >= 20971520 || file.Length == 0) return StatusCode(413);

            string filename = StoreFile(MediaType.ChannelContent, file.OpenReadStream(), new ChannelContentMeta(width, height, MimeTypeMap.GetMimeType(Path.GetExtension(file.FileName)), file.FileName, channel_uuid, file.Length));
            if (filename == "") return StatusCode(500);
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
        private static Image ResizeImage(Image img, int maxWidth, int maxHeight, bool keepAspect)
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

        private string RetreiveMimeType(string content_id)
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
