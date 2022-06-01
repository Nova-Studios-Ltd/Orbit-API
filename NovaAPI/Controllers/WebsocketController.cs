using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NovaAPI.Attri;
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

        [HttpGet("Ping")]
        public ActionResult TestLatency() => StatusCode(200);
        
        
        /*[HttpGet("/TestReconnect/{setAttempts}")]
        [TokenAuthorization]
        public ActionResult TestReconnect(int setAttempts)
        {
            Event.SendReconnectEvent(Context.GetUserUUID(this.GetToken()), setAttempts);
            return StatusCode(200);
        }*/

        [HttpPost("/AllEvents/Event/{event_id}")]
        [TokenAuthorization]
        public ActionResult FireEvent(EventType event_id, string arg1, string arg2)
        {
            try
            {
                Event.Events[event_id](arg1);
            }
            catch
            {
                Event.Events[event_id](arg1, arg2);
            }
            return StatusCode(200);
        }
    }
}
