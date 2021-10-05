using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NovaAPI.Attri;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NovaAPI.Util;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    [Route("Events")]
    [ApiController]
    //[TokenAuthorization]
    public class WebsocketController : ControllerBase
    {
        private readonly ILogger<WebsocketController> _logger;
        private readonly EventManager Event;
        private readonly NovaChatDatabaseContext Context;

        public WebsocketController(ILogger<WebsocketController> logger, EventManager em, NovaChatDatabaseContext context)
        {
            _logger = logger;
            Event = em;
            Context = context;
        }

        [HttpGet("Listen")]
        public async Task ConnectWebsocket(string user_uuid)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                TaskCompletionSource<object> socketFinished = new();
                Event.AddClient(user_uuid, new UserSocket(socketFinished, await HttpContext.WebSockets.AcceptWebSocketAsync()));
                _logger.Log(LogLevel.Information, "WebSocket connection established");
                await socketFinished.Task;
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
            }
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
