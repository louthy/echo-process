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

using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Practices.ServiceLocation;
using System;

namespace Owin.WebSocket
{
    public class WebSocketConnectionMiddleware<T> : OwinMiddleware where T : WebSocketConnection
    {
        private readonly Regex mMatchPattern;
        private readonly IServiceLocator mServiceLocator;
        private static readonly Task completedTask = Task.FromResult(false);

        public WebSocketConnectionMiddleware(OwinMiddleware next, IServiceLocator locator)
            : base(next)
        {
            mServiceLocator = locator;
        }

        public WebSocketConnectionMiddleware(OwinMiddleware next, IServiceLocator locator, Regex matchPattern)
            : this(next, locator)
        {
            mMatchPattern = matchPattern;
        }

        public override Task Invoke(IOwinContext context)
        {
            var matches = new Dictionary<string, string>();

            if (mMatchPattern != null)
            {
                var match = mMatchPattern.Match(context.Request.Path.Value);
                if(!match.Success)
                    return Next?.Invoke(context) ?? completedTask;

                for (var i = 1; i <= match.Groups.Count; i++)
                {
                    var name  = mMatchPattern.GroupNameFromNumber(i);
                    var value = match.Groups[i];
                    matches.Add(name, value.Value);
                }
            }
            
            T socketConnection;
            if(mServiceLocator == null)
                socketConnection = Activator.CreateInstance<T>();
            else
                socketConnection = mServiceLocator.GetInstance<T>();

            return socketConnection.AcceptSocketAsync(context, matches);
        }
    }
}