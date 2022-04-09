using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using NovaAPI.Attri;
using NovaAPI.Util;

namespace NovaAPI.Controllers
{
    [ApiController]
    public class KeystoreController : ControllerBase
    {
        private readonly NovaChatDatabaseContext Context;

        public KeystoreController(NovaChatDatabaseContext context)
        {
            Context = context;
        }
        
        [HttpGet("/User/@me/Keystore")]
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
        
        [HttpGet("/User/@me/Keystore/{Key_user_uuid}")]
        //[TokenAuthorization]
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
        
        [HttpPost("/User/@me/Keystore/{key_user_uuid}")]
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
            using MySqlCommand updateTimestamp = new($"$INSERT INTO `{user_uuid}_keystore` (UUID, PubKey) VALUES (`Timestamp`, @key)", conn);
            updateTimestamp.Parameters.AddWithValue("@key", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            if (updateTimestamp.ExecuteNonQuery() == 0) return StatusCode(409);
            return StatusCode(200);
        }
    }
}