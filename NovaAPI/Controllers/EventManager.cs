using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using NovaAPI.Attri;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    public enum EventType { MessageSent, MessageDelete, MessageEdit, ChannelCreated, ChannelDeleted, GroupNewMember, UserNewGroup, KeyAddedToKeystore, KeyRemoveFromKeystore, RefreshKeystore }
    public class EventManager
    {
        private static readonly Timer Heartbeat = new(CheckPulse, null, 0, 1000 * 30);
        private static readonly Dictionary<string, List<UserSocket>> Clients = new();
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
            foreach (KeyValuePair<string, List<UserSocket>> item in Clients)
            {
                List<int> deadSockets = new List<int>();
                foreach (UserSocket socket in item.Value)
                {
                    if (socket.Socket.State != WebSocketState.Open)
                    {
                        socket.SocketFinished.TrySetResult(null);
                        deadSockets.Add(item.Value.IndexOf(socket));
                        return;
                    }

                    var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {EventType = -1}));
                    await socket.Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length),
                        WebSocketMessageType.Text, true, CancellationToken.None);
                }

                foreach (int i in deadSockets)
                {
                    item.Value.RemoveAt(i);
                }
                if (item.Value.Count == 0) deadClients.Add(item.Key);
            }

            Console.WriteLine($"Removing {deadClients.Count} dead clients");
            foreach (string client in deadClients)
            {
                Clients.Remove(client);
            }
            Console.WriteLine($"Removed {deadClients.Count} dead clients");
        }

        private async void SendEvent(string user_uuid, object eventJson)
        {
            if (Clients.ContainsKey(user_uuid))
            {
                foreach (UserSocket socket in Clients[user_uuid])
                {
                    if (socket.Socket.State == WebSocketState.Open)
                    {
                        var msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(eventJson));
                        await socket.Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
            }
        }
        
        // Message Events
        [WebsocketEvent(EventType.MessageSent)]
        public void MessageSentEvent(string channel_uuid, string message_id)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = removeUsers.ExecuteReader();

            while (reader.Read())
            {
                SendEvent((string)reader["User_UUID"], new { EventType = EventType.MessageSent, Channel = channel_uuid, Message = message_id });
            }
        }
        
        [WebsocketEvent(EventType.MessageDelete)]
        public void MessageDeleteEvent(string channel_uuid, string message_id)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = removeUsers.ExecuteReader();

            while (reader.Read())
            {
                SendEvent((string)reader["User_UUID"], new { EventType = EventType.MessageDelete, Channel = channel_uuid, Message = message_id });
            }
        }
        
        [WebsocketEvent(EventType.MessageEdit)]
        public void MessageEditEvent(string channel_uuid, string message_id)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = removeUsers.ExecuteReader();

            while (reader.Read())
            {
                SendEvent((string)reader["User_UUID"], new { EventType = EventType.MessageEdit, Channel = channel_uuid, Message = message_id });
            }
        }

        // Channel/Group creation event (General purpose event)
        [WebsocketEvent(EventType.ChannelCreated)]
        public void ChannelCreatedEvent(string channel_uuid)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = removeUsers.ExecuteReader();

            while (reader.Read())
            {
                SendEvent((string)reader["User_UUID"], new { EventType = EventType.ChannelCreated, Channel = channel_uuid });
            }
        }
        
        [WebsocketEvent(EventType.ChannelDeleted)]
        public void ChannelDeleteEvent(string channel_uuid)
        {
            using MySqlConnection conn = Context.GetChannels();
            conn.Open();
            using MySqlCommand removeUsers = new($"SELECT * FROM `access_{channel_uuid}`", conn);
            MySqlDataReader reader = removeUsers.ExecuteReader();

            while (reader.Read())
            {
                SendEvent((string)reader["User_UUID"], new { EventType = EventType.ChannelCreated, Channel = channel_uuid });
            }
        }
        
        public void ChannelDeleteEvent(string channel_uuid, string user_uuid)
        {
            if (!Clients.ContainsKey(user_uuid)) return;
            SendEvent(user_uuid, new { EventType = EventType.ChannelDeleted, Channel = channel_uuid });
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
                SendEvent((string)reader["User_UUID"], new { EventType = EventType.GroupNewMember, User = user_uuid });
            }
        }

        [WebsocketEvent(EventType.UserNewGroup)]
        public async void UserNewGroup(string channel_uuid, string user_uuid)
        {
            if (Clients.ContainsKey(user_uuid))
            {
                SendEvent(user_uuid, new { EventType = EventType.UserNewGroup, Channel = channel_uuid });
            }
        }


        public void RemoveClient(string user_uuid)
        {
            if (!Clients.ContainsKey(user_uuid)) return;
            foreach (UserSocket socket in Clients[user_uuid])
            {
                socket.SocketFinished.TrySetResult(null);
            }
            Clients.Remove(user_uuid);
        }
        public async void AddClient(string user_uuid, UserSocket socket)
        {
            //GlobalUtils.ClientSync.WaitOne();
            if (Clients.ContainsKey(user_uuid))
            {
                Clients[user_uuid].Add(socket);
            }
            else
            {
                byte[] buffer = new byte[1024];
                await socket.Socket.ReceiveAsync(buffer, CancellationToken.None);

                string token = Encoding.UTF8.GetString(buffer).Trim();
                if (Context.GetUserUUID(token) != user_uuid && !string.IsNullOrWhiteSpace(token))
                {
                    socket.SocketFinished.TrySetResult(null);
                    Console.WriteLine($"Unable to auth user with uuid {user_uuid}");
                    socket.Socket.Abort();
                }
                
                // Read from websocket, may use this for more later, but for now its just for pinging
                /*Task.Run(async () =>
                {
                    while (true)
                    {
                        byte[] dataBuffer = new byte[4];
                        await socket.Socket.ReceiveAsync(dataBuffer, CancellationToken.None);
                        string data = Encoding.UTF8.GetString(dataBuffer).Trim();
                        //Console.WriteLine(data);
                        if (data == "ping")
                        {
                            var msg = Encoding.UTF8.GetBytes("pong");
                            await socket.Socket.SendAsync(new ArraySegment<byte>(msg, 0, msg.Length),
                                WebSocketMessageType.Text, true, CancellationToken.None);
                        }

                        if (socket.Socket.State == WebSocketState.Closed) return;
                    }
                });*/

                Clients.Add(user_uuid, new List<UserSocket>());
                Clients[user_uuid].Add(socket);
            }
        }

        // Keystore events
        [WebsocketEvent(EventType.RefreshKeystore)]
        public async void RefreshKeystore(string user_uuid)
        {
            if (Clients.ContainsKey(user_uuid))
            {
                SendEvent(user_uuid, new { EventType = EventType.RefreshKeystore });
            }
        }
        [WebsocketEvent(EventType.KeyAddedToKeystore)]
        public async void KeyAddedToKeystore(string user_uuid, string key_user_uuid)
        {
            if (Clients.ContainsKey(user_uuid))
            {
                SendEvent(user_uuid, new { EventType = EventType.KeyAddedToKeystore, KeyUserUUID = key_user_uuid });
            }
        }
        [WebsocketEvent(EventType.KeyRemoveFromKeystore)]
        public async void KeyRemovedFromKeystore(string user_uuid, string key_user_uuid)
        {
            if (Clients.ContainsKey(user_uuid))
            {
                SendEvent(user_uuid, new { EventType = EventType.KeyRemoveFromKeystore, KeyUserUUID = key_user_uuid });
            }
        }


        // Random testing stuff
        /*public async void SendReconnectEvent(string user_uuid, int attempts)
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
        }+*/
    }
}
