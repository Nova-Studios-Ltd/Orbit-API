using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NovaAPI.Controllers
{
    public class EventManager
    {
        private readonly Dictionary<string, WebSocket> Clients;
        private readonly NovaChatDatabaseContext Context;

        public EventManager(NovaChatDatabaseContext context)
        {
            Clients = new Dictionary<string, WebSocket>();
            Context = context;   
        }

        public void MessageSentEvent(string channel_uuid)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = removeUsers.ExecuteReader();

            while (reader.Read())
            {
                Clients[(string)reader["User_UUID"]].SendAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { Channel = channel_uuid })), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public void AddClient(string user_uuid, WebSocket socket) => Clients.Add(user_uuid, socket);
    }
}
