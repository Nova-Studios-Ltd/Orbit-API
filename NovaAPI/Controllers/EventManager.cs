using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using NovaAPI.Models;
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
        private static readonly Dictionary<string, UserSocket> Clients = new();
        private readonly NovaChatDatabaseContext Context;

        public EventManager(NovaChatDatabaseContext context)
        {
            Context = context;   
        }

        public async void MessageSentEvent(string channel_uuid)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = removeUsers.ExecuteReader();

            while (reader.Read())
            {
                if (Clients.ContainsKey((string)reader["User_UUID"])) {
                    var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = 0, Channel = channel_uuid }));
                    if (Clients[(string)reader["User_UUID"]].Socket.State == WebSocketState.Open)
                    {
                        await Clients[(string)reader["User_UUID"]].Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        Clients[(string)reader["User_UUID"]].SocketFinished.TrySetResult(null);
                        Clients.Remove((string)reader["User_UUID"]);
                    }
                }
            }
        }

        public void AddClient(string user_uuid, UserSocket socket)
        { 
            Clients.Add(user_uuid, socket);
            //Echo(socket);
        }


        private async Task Echo(WebSocket webSocket)
        {
            var buffer = new byte[1024];
            var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

            while (!result.CloseStatus.HasValue)
            {
                var serverMsg = Encoding.UTF8.GetBytes($"Server: Hello. You said: {Encoding.UTF8.GetString(buffer)}");
                await webSocket.SendAsync(new ArraySegment<byte>(serverMsg, 0, serverMsg.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);

                result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
    }
}
