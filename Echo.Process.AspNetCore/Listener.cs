using System;
using System.Text;
using Echo.Client;
using LanguageExt;
using Newtonsoft.Json;
using System.Threading;
using static Echo.Process;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using static LanguageExt.Prelude;

namespace Echo
{
    internal sealed class ProcessSysListener
    {
        readonly HttpContext context;
        //private readonly TaskQueue sendQueue;

        public ProcessSysListener(HttpContext context) =>
            this.context = context;

        public async Task Listen()
        {
            using (var webSocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false))
            {
                var buffer = new byte[1024 * 64];

                try
                {
                    do
                    {
                        var result = await ReceiveMessage(webSocket, buffer).ConfigureAwait(false);
                        if (result.Message.Count > 0)
                        {
                            await OnMessageReceived(webSocket, result.Message, result.MessageType).ConfigureAwait(false);
                        }

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }

                    } while (true);
                }
                finally
                {
                    await webSocket.CloseAsync(
                        webSocket.CloseStatus ?? WebSocketCloseStatus.NormalClosure, 
                        webSocket.CloseStatusDescription, 
                        CancellationToken.None)
                       .ConfigureAwait(false);
                }

                webSocket.Dispose();
            }
        }

        public async Task<(ArraySegment<byte> Message, WebSocketMessageType MessageType)> ReceiveMessage(WebSocket webSocket, byte[] buffer)
        {
            var count = 0;
            WebSocketReceiveResult result;
            do
            {
                var segment = new ArraySegment<byte>(buffer, count, buffer.Length - count);
                result =  await webSocket.ReceiveAsync(segment, CancellationToken.None).ConfigureAwait(false);
                count  += result.Count;
            }
            while (!result.EndOfMessage);

            return (new ArraySegment<byte>(buffer, 0, count), result.MessageType);
        }

        static ProcessId FixRootName(ProcessId pid) =>
            pid.Take(1).Name.Value == "root"
                ? Root().Append(pid.Skip(1))
                : pid;

        async Task OnMessageReceived(WebSocket webSocket, ArraySegment<byte> message, WebSocketMessageType type) =>
            await Req.Parse(
                          Encoding.UTF8.GetString(message.Array ?? new byte[0], message.Offset, message.Count),
                          context.Connection.RemoteIpAddress.ToString(),
                          ProcessHub.Connections)
                     .MatchAsync(
                          RightAsync: msg => Echo.Client.ClientMessaging.Write(msg, context.Connection.RemoteIpAddress, SendText(webSocket)),
                          Left: err => {
                                    logUserErr(err);
                                    return unit;
                                })
                     .ConfigureAwait(false);

        public Func<byte[], bool, Task> SendText(WebSocket webSocket) =>
            (byte[] buffer, bool endOfMessage) =>
                webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, endOfMessage, CancellationToken.None);
    }
}