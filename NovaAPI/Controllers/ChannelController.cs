using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using MySql.Data.MySqlClient;
using NovaAPI.Attri;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Controllers
{
    [Route("Channel")]
    [ApiController]
    [TokenAuthorization]
    public class ChannelController : ControllerBase
    {
        private readonly NovaChatDatabaseContext Context;
        public ChannelController(NovaChatDatabaseContext context)
        {
            Context = context;
        }

        [HttpPost("CreateChannel")]
        public string CreateChannel()
        {
            string table_id = Guid.NewGuid().ToString("N");
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();

                // Create table to hold sent messages
                using MySqlCommand createTable = new($"CREATE TABLE `{table_id}` (Message_UUID CHAR(255) NOT NULL, Author_UUID CHAR(255) NOT NULL, Content VARCHAR(4000) NOT NULL, CreationDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, UNIQUE Message_IDs(Message_UUID)) ENGINE = InnoDB; ", conn);
                createTable.ExecuteNonQuery();

                // Add table id to channels table
                using MySqlCommand addChannel = new($"INSERT INTO `Channels` (`Table_ID`, `Owner_UUID`, `Timestamp`) VALUES (@table_id, @owner_uuid, CURRENT_TIMESTAMP)", conn);
                addChannel.Parameters.AddWithValue("@table_id", table_id);
                addChannel.Parameters.AddWithValue("@owner_uuid", Context.GetUserUUID(GetToken()));
                addChannel.ExecuteNonQuery();
            }

            return table_id;
        }

        [HttpDelete("DeleteChannel/{channel_uuid}")]
        public ActionResult DeleteChannel(string channel_uuid)
        {
            using (MySqlConnection conn = Context.GetChannels())
            {
                conn.Open();

                // Remove channel from Channels table
                using MySqlCommand cmd = new($"DELETE FROM Channels WHERE (Table_ID=@table_id) AND (Owner_UUID=@owner_uuid)", conn);
                cmd.Parameters.AddWithValue("@table_id", channel_uuid);
                cmd.Parameters.AddWithValue("@owner_uuid", Context.GetUserUUID(GetToken()));
                if (cmd.ExecuteNonQuery() == 0) return NotFound();

                // Remove channel table (removing messages)
                using MySqlCommand deleteChannel = new($"DROP TABLE `{channel_uuid}`", conn);
                deleteChannel.ExecuteNonQuery();
            }
            return NoContent();
        }

        string GetToken()
        {
            if (!Request.Headers.TryGetValue("Authorization", out StringValues values))
                return "";
            return values.First();
        }
    }
}
