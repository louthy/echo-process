using System;
using System.Net.WebSockets;
using System.Threading;

namespace Owin.WebSocket.Handlers
{
    internal class SendContext
    {
        public ArraySegment<byte> Buffer;
        public bool EndOfMessage;
        public WebSocketMessageType Type;
        public CancellationToken CancelToken;

        public SendContext(ArraySegment<byte> buffer, bool endOfMessage, WebSocketMessageType type, CancellationToken cancelToken)
        {
            Buffer = buffer;
            EndOfMessage = endOfMessage;
            Type = type;
            CancelToken = cancelToken;
        }
    }
}