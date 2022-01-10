using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using NovaAPI.Attri;
using NovaAPI.Models;
using NovaAPI.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NovaAPI.Controllers
{
    public enum EventType { MessageSent, MessageDelete, MessageEdit, ChannelCreated, ChannelDeleted, GroupNewMember, UserNewGroup, KeyAddedToKeystore, KeyRemoveFromKeystore, RefreshKeystore }
    public class EventManager
    {
        private static readonly Timer Heartbeat = new(CheckPulse, null, 0, 1000 * 30);
        private static readonly Dictionary<string, UserSocket> Clients = new();
        private readonly NovaChatDatabaseContext Context;

        public delegate void Single(string arg1);
        public delegate void Double(string arg1, string arg2);

        public Dictionary<EventType, dynamic> Events = new();

        public EventManager(NovaChatDatabaseContext context)
        {
            Context = context;
            MethodInfo[] methods = Assembly.GetExecutingAssembly().GetTypes()
                      .SelectMany(t => t.GetMethods())
                      .Where(m => m.GetCustomAttributes(typeof(WebsocketEvent), false).Length > 0)
                      .ToArray();
            foreach (MethodInfo method in methods)
            {
                WebsocketEvent we = method.GetCustomAttribute<WebsocketEvent>();
                if (we == null) continue;
                if (method.GetParameters().Length == 1)
                    Events[we.Type] = method.CreateDelegate(typeof(Single), this);
                else
                    Events[we.Type] = method.CreateDelegate(typeof(Double), this);
            }
        }

        public async static void CheckPulse(object state)
        {
            List<string> deadClients = new();
            Console.WriteLine("Checking for dead clients...");
            foreach (KeyValuePair<string, UserSocket> item in Clients)
            {
                if (item.Value.Socket.State != WebSocketState.Open)
                {
                    item.Value.SocketFinished.TrySetResult(null);
                    deadClients.Add(item.Key);
                }

                var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = -1}));
                await item.Value.Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

                /*try
                {
                    byte[] buffer = new byte[1024];
                    CancellationToken timeout = new CancellationTokenSource(1500).Token;
                    await item.Value.Socket.ReceiveAsync(buffer, timeout);
                    if (Encoding.UTF8.GetString(buffer).Trim() != "<Beep>")
                    {
                        item.Value.SocketFinished.TrySetResult(null);
                        deadClients.Add(item.Key);
                    }
                }
                catch (OperationCanceledException)
                {
                    item.Value.SocketFinished.TrySetResult(null);
                    deadClients.Add(item.Key);
                }*/
            }

            Console.WriteLine($"Removing {deadClients.Count} dead clients");
            foreach (string client in deadClients)
            {
                Clients.Remove(client);
            }
            Console.WriteLine($"Removed {deadClients.Count} dead clients");
        }

        // Message Events
        [WebsocketEvent(EventType.MessageSent)]
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
                        RemoveClient((string)reader["User_UUID"]);
                    }
                }
            }
        }
        [WebsocketEvent(EventType.MessageDelete)]
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
                        RemoveClient((string)reader["User_UUID"]);
                    }
                }
            }
        }
        [WebsocketEvent(EventType.MessageEdit)]
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
                        RemoveClient((string)reader["User_UUID"]);
                    }
                }
            }
        }

        // Channel/Group creation event (General purpose event)
        [WebsocketEvent(EventType.ChannelCreated)]
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
                        RemoveClient((string)reader["User_UUID"]);
                    }
                }
            }
        }
        [WebsocketEvent(EventType.ChannelDeleted)]
        public async void ChannelDeleteEvent(string channel_uuid)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = removeUsers.ExecuteReader();

            while (reader.Read())
            {
                if (Clients.ContainsKey((string)reader["User_UUID"]))
                {
                    var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = EventType.ChannelDeleted, Channel = channel_uuid }));
                    if (Clients[(string)reader["User_UUID"]].Socket.State == WebSocketState.Open)
                    {
                        await Clients[(string)reader["User_UUID"]].Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        RemoveClient((string)reader["User_UUID"]);
                    }
                }
            }
        }
        
        public async void ChannelDeleteEvent(string channel_uuid, string user_uuid)
        {
            if (!Clients.ContainsKey(user_uuid)) return;
            var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = EventType.ChannelDeleted, Channel = channel_uuid }));
            if (Clients[user_uuid].Socket.State == WebSocketState.Open)
            {
                await Clients[user_uuid].Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                RemoveClient(user_uuid);
            }
        }

        [WebsocketEvent(EventType.GroupNewMember)]
        public async void GroupNewMember(string channel_uuid, string user_uuid)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = removeUsers.ExecuteReader();

            while (reader.Read())
            {
                if (Clients.ContainsKey((string)reader["User_UUID"]) && (string)reader["User_UUID"] != user_uuid)
                {
                    var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = EventType.GroupNewMember, User = user_uuid }));
                    if (Clients[(string)reader["User_UUID"]].Socket.State == WebSocketState.Open)
                    {
                        await Clients[(string)reader["User_UUID"]].Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        RemoveClient((string)reader["User_UUID"]);
                    }
                }
            }
        }

        [WebsocketEvent(EventType.UserNewGroup)]
        public async void UserNewGroup(string channel_uuid, string user_uuid)
        {
            if (Clients.ContainsKey(user_uuid))
            {
                var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = EventType.UserNewGroup, Channel = channel_uuid }));
                if (Clients[user_uuid].Socket.State == WebSocketState.Open)
                {
                    await Clients[user_uuid].Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    RemoveClient(user_uuid);
                }
            }
        }


        public void RemoveClient(string user_uuid)
        {
            //GlobalUtils.ClientSync.WaitOne();
            Clients[user_uuid].SocketFinished.TrySetResult(null);
            Clients.Remove(user_uuid);
            //GlobalUtils.ClientSync.ReleaseMutex();
        }
        public async void AddClient(string user_uuid, UserSocket socket)
        {
            //GlobalUtils.ClientSync.WaitOne();
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

                string token = Encoding.UTF8.GetString(buffer).Trim();
                if (Context.GetUserUUID(token) != user_uuid && !string.IsNullOrWhiteSpace(token))
                {
                    socket.SocketFinished.TrySetResult(null);
                    socket.Socket.Abort();
                }

                Clients.Add(user_uuid, socket);
            }
            //GlobalUtils.ClientSync.ReleaseMutex();
            //Echo(socket);
        }

        // Keystore events
        [WebsocketEvent(EventType.RefreshKeystore)]
        public async void RefreshKeystore(string user_uuid)
        {
            if (Clients.ContainsKey(user_uuid))
            {
                var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = EventType.RefreshKeystore }));
                if (Clients[user_uuid].Socket.State == WebSocketState.Open)
                {
                    await Clients[user_uuid].Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    RemoveClient(user_uuid);
                }
            }
        }
        [WebsocketEvent(EventType.KeyAddedToKeystore)]
        public async void KeyAddedToKeystore(string user_uuid, string key_user_uuid)
        {
            if (Clients.ContainsKey(user_uuid))
            {
                var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = EventType.KeyAddedToKeystore, KeyUserUUID = key_user_uuid }));
                if (Clients[user_uuid].Socket.State == WebSocketState.Open)
                {
                    await Clients[user_uuid].Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    RemoveClient(user_uuid);
                }
            }
        }
        [WebsocketEvent(EventType.KeyRemoveFromKeystore)]
        public async void KeyRemovedFromKeystore(string user_uuid, string key_user_uuid)
        {
            if (Clients.ContainsKey(user_uuid))
            {
                var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = EventType.KeyRemoveFromKeystore, KeyUserUUID = key_user_uuid }));
                if (Clients[user_uuid].Socket.State == WebSocketState.Open)
                {
                    await Clients[user_uuid].Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    RemoveClient(user_uuid);
                }
            }
        }


        // Random testing stuff
        public async void SendReconnectEvent(string user_uuid, int attempts)
        {
            if (Clients.ContainsKey(user_uuid))
            {
                var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { EventType = 420, Attempts = attempts }));
                if (Clients[user_uuid].Socket.State == WebSocketState.Open)
                {
                    await Clients[user_uuid].Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    Clients[user_uuid].SocketFinished.TrySetResult(null);
                    Clients.Remove(user_uuid);
                }
            }
        }
    }
}
