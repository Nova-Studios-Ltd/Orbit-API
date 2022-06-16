using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using NovaAPI.Attri;
using NovaAPI.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Controllers
{
    [Route("Friend")]
    [ApiController]
    [TokenAuthorization]
    public class FriendController : ControllerBase
    {
        private readonly NovaChatDatabaseContext Context;

        public FriendController(NovaChatDatabaseContext context)
        {
            Context = context;
        }


        [HttpGet("{user_uuid}/Friends")]
        public ActionResult<Dictionary<string, string>> GetFriends(string user_uuid)
        {
            CheckTable(user_uuid);
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();

            using MySqlCommand getFriends = new($"SELECT * FROM `{user_uuid}_friends`", conn);
            MySqlDataReader reader = getFriends.ExecuteReader();
            Dictionary<string, string> friends = new();
            while (reader.Read())
            {
                friends.Add(reader["UUID"].ToString(), reader["State"].ToString());
            }
            return friends;
        }

        [HttpPost("{user_uuid}/Send/{request_uuid}")]
        public ActionResult SendRequest(string user_uuid, string request_uuid)
        {
            CheckTable(user_uuid);
            CheckTable(request_uuid);
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();

            using MySqlCommand setRequest = new($"INSERT INTO `{request_uuid}_friends` (UUID, State) VALUES (@uuid, @state)", conn);
            setRequest.Parameters.AddWithValue("@uuid", user_uuid);
            setRequest.Parameters.AddWithValue("@state", "Request");
            setRequest.ExecuteNonQuery();

            using MySqlCommand setFriend = new($"INSERT INTO `{user_uuid}_friends` (UUID, State) VALUES (@uuid, @state)", conn);
            setFriend.Parameters.AddWithValue("@uuid", request_uuid);
            setFriend.Parameters.AddWithValue("@state", "Pending");
            setFriend.ExecuteNonQuery();

            return StatusCode(200);
        }

        [HttpPatch("{user_uuid}/Accept/{request_uuid}")]
        public ActionResult AcceptRequest(string user_uuid, string request_uuid)
        {
            if (Context.GetUserUUID(this.GetToken()) != user_uuid) return StatusCode(401);
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();        

            using MySqlCommand setRequest = new($"UPDATE `{request_uuid}_friends` SET State=@state WHERE UUID=@uuid", conn);
            setRequest.Parameters.AddWithValue("@uuid", user_uuid);
            setRequest.Parameters.AddWithValue("@state", "Accepted");
            setRequest.ExecuteNonQuery();

            using MySqlCommand setFriend = new($"UPDATE `{user_uuid}_friends` SET State=@state WHERE UUID=@uuid", conn);
            setFriend.Parameters.AddWithValue("@uuid", request_uuid);
            setFriend.Parameters.AddWithValue("@state", "Accepted");
            setFriend.ExecuteNonQuery();

            return StatusCode(200);
        }

        [HttpPatch("{user_uuid}/Decline/{request_uuid}")]
        public ActionResult DeclineRequest(string user_uuid, string request_uuid)
        {
            if (Context.GetUserUUID(this.GetToken()) != user_uuid) return StatusCode(401);
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();

            using MySqlCommand setRequest = new($"DELETE FROM `{request_uuid}_friends` WHERE UUID=@uuid", conn);
            setRequest.Parameters.AddWithValue("@uuid", user_uuid);
            setRequest.Parameters.AddWithValue("@state", "Request");
            setRequest.ExecuteNonQuery();

            using MySqlCommand setFriend = new($"DELETE FROM `{user_uuid}_friends` WHERE UUID=@uuid", conn);
            setFriend.Parameters.AddWithValue("@uuid", request_uuid);
            setFriend.Parameters.AddWithValue("@state", "Pending");
            setFriend.ExecuteNonQuery();

            return StatusCode(200);
        }

        private void CheckTable(string user_uuid)
        {
            using MySqlConnection conn = Context.GetUsers();
            conn.Open();

            using MySqlCommand createFriends = new($"CREATE TABLE IF NOT EXISTS `{user_uuid}_friends` (Id INT NOT NULL AUTO_INCREMENT, UUID CHAR(255) NOT NULL, State CHAR(255) NOT NULL, PRIMARY KEY (`Id`), UNIQUE (`UUID`)) ENGINE = InnoDB;", conn);
            createFriends.ExecuteNonQuery();
        }
    }
}
