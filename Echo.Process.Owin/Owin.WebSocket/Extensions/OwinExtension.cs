// The MIT License (MIT)
//
// Copyright (c) 2014 Bryce
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// https://github.com/bryceg/Owin.WebSocket/blob/master/LICENSE

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Practices.ServiceLocation;

namespace Owin.WebSocket.Extensions
{
    public static class OwinExtension
    {
        /// <summary>
        /// Maps a static URI to a web socket consumer
        /// </summary>
        /// <typeparam name="T">Type of WebSocketHubConnection</typeparam>
        /// <param name="app">Owin App</param>
        /// <param name="route">Static URI to map to the hub</param>
        /// <param name="serviceLocator">Service locator to use for getting instances of T</param>
        public static void MapWebSocketRoute<T>(this IAppBuilder app, string route, IServiceLocator serviceLocator = null) 
            where T : WebSocketConnection
        {
            app.Map(route, config => config.Use<WebSocketConnectionMiddleware<T>>(serviceLocator));
        }

        /// <summary>
        /// Maps a URI pattern to a web socket consumer using a Regex pattern mach on the URI
        /// </summary>
        /// <typeparam name="T">Type of WebSocketHubConnection</typeparam>
        /// <param name="app">Owin app</param>        /// 
        /// <param name="regexPatternMatch">Regex pattern of the URI to match.  Capture groups will be sent to the hub on the Arguments property</param>
        /// <param name="serviceLocator">Service locator to use for getting instances of T</param>
        public static void MapWebSocketPattern<T>(this IAppBuilder app, string regexPatternMatch, IServiceLocator serviceLocator = null)
            where T : WebSocketConnection
        {
            app.Use<WebSocketConnectionMiddleware<T>>(serviceLocator, new Regex(regexPatternMatch, RegexOptions.Compiled | RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// Maps a static URI route to the web socket connection using the WebSocketRouteAttribute
        /// </summary>
        /// <typeparam name="T">Type of WebSocketHubConnection</typeparam>
        /// <param name="app">Owin App</param>
        /// <param name="serviceLocator">Service locator to use for getting instances of T</param>
        public static void MapWebSocketRoute<T>(this IAppBuilder app, IServiceLocator serviceLocator = null)
            where T : WebSocketConnection
        {
            var routeAttributes = typeof(T).GetCustomAttributes(typeof(WebSocketRouteAttribute), true);

            if (routeAttributes.Length == 0)
                throw new InvalidOperationException(typeof(T).Name + " type must have attribute of WebSocketRouteAttribute for mapping");

            foreach (var routeAttribute in routeAttributes.Cast<WebSocketRouteAttribute>())
            {
                app.Map(routeAttribute.Route, config => config.Use<WebSocketConnectionMiddleware<T>>(serviceLocator));
            }
        }

        internal static T Get<T>(this IDictionary<string, object> dictionary, string key)
        {
            object item;
            if (dictionary.TryGetValue(key, out item))
            {
                return (T) item;
            }

            return default(T);
        }
    }
}
