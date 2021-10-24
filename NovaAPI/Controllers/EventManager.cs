using Microsoft.Extensions.Logging;
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
        enum EventType { MessageSent, MessageDelete, MessageEdit, ChannelCreated }
        private static readonly Timer Heartbeat = new(CheckPulse, null, 0, 1000 * 10);
        private static readonly Dictionary<string, UserSocket> Clients = new();
        private readonly NovaChatDatabaseContext Context;

        public EventManager(NovaChatDatabaseContext context)
        {
            Context = context;
        }

        public static void CheckPulse(object state)
        {
            List<string> deadClients = new();
            foreach (KeyValuePair<string, UserSocket> item in Clients)
            {
                if (item.Value.Socket.State != WebSocketState.Open)
                {
                    item.Value.SocketFinished.TrySetResult(null);
                    deadClients.Add(item.Key);
                }

                //var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = -1}));
                //await item.Value.Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }

            foreach (string client in deadClients)
            {
                Clients.Remove(client);
            }
        }

        // Message Events
        public async void MessageSentEvent(string channel_uuid, string message_id)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = removeUsers.ExecuteReader();

            while (reader.Read())
            {
                if (Clients.ContainsKey((string)reader["User_UUID"])) {
                    var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = EventType.MessageSent, Channel = channel_uuid, Message = message_id }));
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
        public async void MessageDeleteEvent(string channel_uuid, string message_id)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = removeUsers.ExecuteReader();

            while (reader.Read())
            {
                if (Clients.ContainsKey((string)reader["User_UUID"]))
                {
                    var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = EventType.MessageDelete, Channel = channel_uuid, Message = message_id }));
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
        public async void MessageEditEvent(string channel_uuid, string message_id)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = removeUsers.ExecuteReader();

            while (reader.Read())
            {
                if (Clients.ContainsKey((string)reader["User_UUID"]))
                {
                    var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = EventType.MessageEdit, Channel = channel_uuid, Message = message_id }));
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

        // Channel Events
        public async void ChannelCreatedEvent(string channel_uuid)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = removeUsers.ExecuteReader();

            while (reader.Read())
            {
                if (Clients.ContainsKey((string)reader["User_UUID"]))
                {
                    var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = EventType.ChannelCreated, Channel = channel_uuid }));
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

        public async void AddClient(string user_uuid, UserSocket socket)
        {
            if (Clients.ContainsKey(user_uuid))
            {
                Clients[user_uuid].SocketFinished.TrySetResult(null);
                Clients.Remove(user_uuid);
                Clients.Add(user_uuid, socket);
            }
            else
            {
                byte[] buffer = new byte[1024];
                WebSocketReceiveResult result = await socket.Socket.ReceiveAsync(buffer, CancellationToken.None);
                Console.WriteLine(Encoding.UTF8.GetString(buffer).Trim());

                string token = Encoding.UTF8.GetString(buffer).Trim();
                if (Context.GetUserUUID(token) != user_uuid && !string.IsNullOrWhiteSpace(token))
                {
                    socket.SocketFinished.TrySetResult(null);
                    socket.Socket.Abort();
                }

                Clients.Add(user_uuid, socket);
            }
            //Echo(socket);
        }
    }
}
