using NovaAPI.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Attri
{
    public class WebsocketEvent : Attribute
    {
        public EventType Type;
        public WebsocketEvent(EventType type)
        {
            Type = type;
        }
    }
}
