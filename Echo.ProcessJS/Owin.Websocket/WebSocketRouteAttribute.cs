using System;

namespace Owin.WebSocket
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=true)]
    public class WebSocketRouteAttribute: Attribute
    {
        public string Route { get; set; }

        public WebSocketRouteAttribute(string route)
        {
            Route = route;
        }
    }
}
