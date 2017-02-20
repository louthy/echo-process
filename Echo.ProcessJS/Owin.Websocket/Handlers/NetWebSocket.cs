using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Owin.WebSocket.Extensions;

namespace Owin.WebSocket.Handlers
{
    class NetWebSocket: IWebSocket
    {
        private readonly TaskQueue mSendQueue;
        private readonly System.Net.WebSockets.WebSocket mWebSocket;

        public NetWebSocket(System.Net.WebSockets.WebSocket webSocket)
        {
            mWebSocket = webSocket;
            mSendQueue = new TaskQueue();
        }

        public TaskQueue SendQueue
        {
            get { return mSendQueue; }
        }

        public WebSocketCloseStatus? CloseStatus
        {
            get { return mWebSocket.CloseStatus; }
        }

        public string CloseStatusDescription
        {
            get { return mWebSocket.CloseStatusDescription; }
        }

        public Task SendText(ArraySegment<byte> data, bool endOfMessage, CancellationToken cancelToken)
        {
            return Send(data, WebSocketMessageType.Text, endOfMessage, cancelToken);
        }

        public Task SendBinary(ArraySegment<byte> data, bool endOfMessage, CancellationToken cancelToken)
        {
            return Send(data, WebSocketMessageType.Binary, endOfMessage, cancelToken);
        }

        public Task Send(ArraySegment<byte> data, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancelToken)
        {
            var sendContext = new SendContext(data, endOfMessage, messageType, cancelToken);

            return mSendQueue.Enqueue(
                async s =>
                {
                    await mWebSocket.SendAsync(s.Buffer, s.Type, s.EndOfMessage, s.CancelToken);
                },
                sendContext);
        }

        public Task Close(WebSocketCloseStatus closeStatus, string closeDescription, CancellationToken cancelToken)
        {
            return mWebSocket.CloseAsync(closeStatus, closeDescription, cancelToken);
        }
        
        public async Task<Tuple<ArraySegment<byte>, WebSocketMessageType>> ReceiveMessage(byte[] buffer, CancellationToken cancelToken)
        {
            var count = 0;
            WebSocketReceiveResult result;
            do
            {
                var segment = new ArraySegment<byte>(buffer, count, buffer.Length - count);
                result = await mWebSocket.ReceiveAsync(segment, cancelToken);

                count += result.Count;
            }
            while (!result.EndOfMessage);

            return new Tuple<ArraySegment<byte>, WebSocketMessageType>(new ArraySegment<byte>(buffer, 0, count), result.MessageType);
        }
    }
}
