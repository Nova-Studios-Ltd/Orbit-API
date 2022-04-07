using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace NovaAPI.Models
{
    public class UserSocket
    {
        public TaskCompletionSource<object> SocketFinished;
        public WebSocket Socket;

        public UserSocket(TaskCompletionSource<object> socketFinished, WebSocket socket)
        {
            SocketFinished = socketFinished;
            Socket = socket;
        }
    }
}
