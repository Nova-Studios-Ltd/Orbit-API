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
            if (user_uuid != Context.GetUserUUID(this.GetToken())) return StatusCode(403);
            return FriendUtils.GetFriends(user_uuid);
        }

        [HttpGet("{user_uuid}/Friends/{request_uuid}")]
        public ActionResult<object> GetFriend(string user_uuid, string request_uuid)
        {
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User);
            using MySqlCommand contains = new MySqlCommand($"SELECT * FROM `{user_uuid}_friends` WHERE UUID=@uuid", conn);
            contains.Parameters.AddWithValue("@uuid", request_uuid);
            MySqlDataReader reader = contains.ExecuteReader();
            while (reader.Read())
            {
                return new {UUID = request_uuid, State = reader["State"]};
            }

            return StatusCode(404);
        }

        [HttpPost("{user_uuid}/Send/{request_uuid}")]
        public ActionResult SendRequest(string user_uuid, string request_uuid)
        {
            CheckTable(user_uuid);
            CheckTable(request_uuid);
            if (user_uuid != Context.GetUserUUID(this.GetToken())) return StatusCode(403);
            if (FriendUtils.IsBlocked(user_uuid, request_uuid)) return StatusCode(403);
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User);
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
            if (Context.GetUserUUID(this.GetToken()) != user_uuid) return StatusCode(403);
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User);
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
            if (Context.GetUserUUID(this.GetToken()) != user_uuid) return StatusCode(403);
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User);
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

        [HttpPatch("{user_uuid}/Block/{request_uuid}")]
        public ActionResult BlockRequest(string user_uuid, string request_uuid)
        {
            if (Context.GetUserUUID(this.GetToken()) != user_uuid) return StatusCode(403);
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User);
            conn.Open();
            
            using MySqlCommand setBlocked = new($"INSERT INTO `{request_uuid}_friends` (State, UUID) VALUES (@state, @uuid) ON DUPLICATE KEY UPDATE State=@state", conn);
            setBlocked.Parameters.AddWithValue("@uuid", user_uuid);
            setBlocked.Parameters.AddWithValue("@state", "Blocked");
            setBlocked.ExecuteNonQuery();
            return StatusCode(200);
        }

        [HttpPatch("{user_uuid}/Unblock/{request_uuid}")]
        public ActionResult UnBlockRequest(string user_uuid, string request_uuid)
        {
            if (Context.GetUserUUID(this.GetToken()) != user_uuid) return StatusCode(403);
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User);
            conn.Open();
            
            using MySqlCommand setFriend = new($"DELETE FROM `{user_uuid}_friends` WHERE UUID=@uuid", conn);
            setFriend.Parameters.AddWithValue("@uuid", request_uuid);
            setFriend.ExecuteNonQuery();

            return StatusCode(200);
        }

        [HttpDelete("{user_uuid}/Remove/{request_uuid}")]
        public ActionResult RemoveFriend(string user_uuid, string request_uuid)
        {
            if (Context.GetUserUUID(this.GetToken()) != user_uuid) return StatusCode(403);
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User);
            conn.Open();
            
            using MySqlCommand setFriend = new($"DELETE FROM `{user_uuid}_friends` WHERE UUID=@uuid", conn);
            setFriend.Parameters.AddWithValue("@uuid", request_uuid);
            setFriend.ExecuteNonQuery();

            return StatusCode(200);
        }

        private static void CheckTable(string user_uuid)
        {
            using MySqlConnection conn = MySqlServer.CreateSQLConnection(Database.User);
            conn.Open();

            using MySqlCommand createFriends = new($"CREATE TABLE IF NOT EXISTS `{user_uuid}_friends` (Id INT NOT NULL AUTO_INCREMENT, UUID CHAR(255) NOT NULL, State CHAR(255) NOT NULL, PRIMARY KEY (`Id`), UNIQUE (`UUID`)) ENGINE = InnoDB;", conn);
            createFriends.ExecuteNonQuery();
        }
    }
}
