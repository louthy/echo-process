using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Owin.WebSocket.Extensions;

namespace Owin.WebSocket.Handlers
{
    internal interface IWebSocket
    {
        TaskQueue SendQueue { get; }
        Task SendText(ArraySegment<byte> data, bool endOfMessage, CancellationToken cancelToken);
        Task SendBinary(ArraySegment<byte> data, bool endOfMessage, CancellationToken cancelToken);
        Task Send(ArraySegment<byte> data, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancelToken);
        Task Close(WebSocketCloseStatus closeStatus, string closeDescription, CancellationToken cancelToken);
        Task<Tuple<ArraySegment<byte>, WebSocketMessageType>> ReceiveMessage(byte[] buffer, CancellationToken cancelToken);
        WebSocketCloseStatus? CloseStatus { get; }
        string CloseStatusDescription { get; }
    }
}