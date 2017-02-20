using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Owin.WebSocket.Extensions;

namespace Owin.WebSocket.Handlers
{
    using WebSocketSendAsync =
        Func
        <
            ArraySegment<byte> /* data */,
            int /* messageType */,
            bool /* endOfMessage */,
            CancellationToken /* cancel */,
            Task
        >;

    using WebSocketReceiveAsync =
        Func
        <
            ArraySegment<byte> /* data */,
            CancellationToken /* cancel */,
            Task
            <
                Tuple
                <
                    int /* messageType */,
                    bool /* endOfMessage */,
                    int /* count */
                >
            >
        >;

    using WebSocketCloseAsync =
        Func
        <
            int /* closeStatus */,
            string /* closeDescription */,
            CancellationToken /* cancel */,
            Task
        >;

    internal class OwinWebSocket : IWebSocket
    {
        internal const int CONTINUATION_OP = 0x0;
        internal const int TEXT_OP = 0x1;
        internal const int BINARY_OP = 0x2;
        internal const int CLOSE_OP = 0x8;
        internal const int PONG = 0xA;

        private readonly WebSocketSendAsync mSendAsync;
        private readonly WebSocketReceiveAsync mReceiveAsync;
        private readonly WebSocketCloseAsync mCloseAsync;
        private readonly TaskQueue mSendQueue;

        public TaskQueue SendQueue { get { return mSendQueue;} }

        public WebSocketCloseStatus? CloseStatus { get { return null; } }

        public string CloseStatusDescription { get { return null; } }

        public OwinWebSocket(IDictionary<string,object> owinEnvironment)
        {
            mSendAsync = (WebSocketSendAsync)owinEnvironment["websocket.SendAsync"];
            mReceiveAsync = (WebSocketReceiveAsync)owinEnvironment["websocket.ReceiveAsync"];
            mCloseAsync = (WebSocketCloseAsync)owinEnvironment["websocket.CloseAsync"];
            mSendQueue = new TaskQueue();
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
                    await mSendAsync(s.Buffer, MessageTypeEnumToOpCode(s.Type), s.EndOfMessage, s.CancelToken);
                },
                sendContext);
        }
        
        public Task Close(WebSocketCloseStatus closeStatus, string closeDescription, CancellationToken cancelToken)
        {
            return mCloseAsync((int)closeStatus, closeDescription, cancelToken);
        }

        public async Task<Tuple<ArraySegment<byte>, WebSocketMessageType>> ReceiveMessage(byte[] buffer, CancellationToken cancelToken)
        {
            var count = 0;
            Tuple<int,bool,int> result;
            int opType = -1;
            do
            {
                var segment = new ArraySegment<byte>(buffer, count, buffer.Length - count);
                result = await mReceiveAsync(segment, cancelToken);

                count += result.Item3;
                if (opType == -1)
                    opType = result.Item1;

                if (count == buffer.Length && !result.Item2)
                    throw new InternalBufferOverflowException(
                        "The Buffer is to small to get the Websocket Message! Increase in the Constructor!");
            }
            while (!result.Item2);

            return new Tuple<ArraySegment<byte>, WebSocketMessageType>(new ArraySegment<byte>(buffer, 0, count), MessageTypeOpCodeToEnum(opType));
        }

        private static WebSocketMessageType MessageTypeOpCodeToEnum(int messageType)
        {
            switch (messageType)
            {
                case TEXT_OP:
                    return WebSocketMessageType.Text;
                case BINARY_OP:
                    return WebSocketMessageType.Binary;
                case CLOSE_OP:
                    return WebSocketMessageType.Close;
                case PONG:
                    return WebSocketMessageType.Binary;
                default:
                    throw new ArgumentOutOfRangeException("messageType", messageType, String.Empty);
            }
        }

        private static int MessageTypeEnumToOpCode(WebSocketMessageType webSocketMessageType)
        {
            switch (webSocketMessageType)
            {
                case WebSocketMessageType.Text:
                    return TEXT_OP;
                case WebSocketMessageType.Binary:
                    return BINARY_OP;
                case WebSocketMessageType.Close:
                    return CLOSE_OP;
                default:
                    throw new ArgumentOutOfRangeException("webSocketMessageType", webSocketMessageType, String.Empty);
            }
        }
    }
}
