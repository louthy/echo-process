using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin.WebSocket.Extensions;
using Owin.WebSocket.Handlers;

namespace Owin.WebSocket
{
    public abstract class WebSocketConnection
    {
        private readonly CancellationTokenSource mCancellToken;
        private IWebSocket mWebSocket;

        /// <summary>
        /// Owin context for the web socket
        /// </summary>
        public IOwinContext Context { get; private set; }

        /// <summary>
        /// Maximum message size in bytes for the receive buffer
        /// </summary>
        public int MaxMessageSize { get; private set; }

        /// <summary>
        /// Arguments captured from URI using Regex
        /// </summary>
        public Dictionary<string, string> Arguments { get; private set; }

        /// <summary>
        /// Queue of send operations to the client
        /// </summary>
        public TaskQueue QueueSend { get { return mWebSocket.SendQueue;} }

        protected WebSocketConnection(int maxMessageSize = 1024*64)
        {
            mCancellToken = new CancellationTokenSource();
            MaxMessageSize = maxMessageSize;
        }
        
        /// <summary>
        /// Closes the websocket connection
        /// </summary>
        public Task Close(WebSocketCloseStatus status, string reason)
        {
            return mWebSocket.Close(status, reason, CancellationToken.None);
        }

        /// <summary>
        /// Aborts the websocket connection
        /// </summary>
        public void Abort()
        {
            mCancellToken.Cancel();
        }

        /// <summary>
        /// Sends data to the client with binary message type
        /// </summary>
        /// <param name="buffer">Data to send</param>
        /// <param name="endOfMessage">End of the message?</param>
        /// <returns>Task to send the data</returns>
        public Task SendBinary(byte[] buffer, bool endOfMessage)
        {
            return SendBinary(new ArraySegment<byte>(buffer), endOfMessage);
        }
   
        /// <summary>
        /// Sends data to the client with binary message type
        /// </summary>
        /// <param name="buffer">Data to send</param>
        /// <param name="endOfMessage">End of the message?</param>
        /// <returns>Task to send the data</returns>
        public Task SendBinary(ArraySegment<byte> buffer, bool endOfMessage)
        {
            return mWebSocket.SendBinary(buffer, endOfMessage, CancellationToken.None);
        }

        /// <summary>
        /// Sends data to the client with the text message type
        /// </summary>
        /// <param name="buffer">Data to send</param>
        /// <param name="endOfMessage">End of the message?</param>
        /// <returns>Task to send the data</returns>
        public Task SendText(byte[] buffer, bool endOfMessage)
        {
            return SendText(new ArraySegment<byte>(buffer), endOfMessage);
        }

        /// <summary>
        /// Sends data to the client with the text message type
        /// </summary>
        /// <param name="buffer">Data to send</param>
        /// <param name="endOfMessage">End of the message?</param>
        /// <returns>Task to send the data</returns>
        public Task SendText(ArraySegment<byte> buffer, bool endOfMessage)
        {
            return mWebSocket.SendText(buffer, endOfMessage, CancellationToken.None);
        }

        /// <summary>
        /// Sends data to the client
        /// </summary>
        /// <param name="buffer">Data to send</param>
        /// <param name="endOfMessage">End of the message?</param>
        /// <param name="type">Message type of the data</param>
        /// <returns>Task to send the data</returns>
        public Task Send(ArraySegment<byte> buffer, bool endOfMessage, WebSocketMessageType type)
        {
            return mWebSocket.Send(buffer, type, endOfMessage, CancellationToken.None);
        }
        
        /// <summary>
        /// Verify the request
        /// </summary>
        /// <param name="request">Websocket request</param>
        /// <returns>True if the request is authenticated else false to throw unauthenticated and deny the connection</returns>
        public virtual bool AuthenticateRequest(IOwinRequest request)
        {
            return true;
        }

        /// <summary>
        /// Verify the request asynchronously. Fires after AuthenticateRequest
        /// </summary>
        /// <param name="request">Websocket request</param>
        /// <returns>True if the request is authenticated else false to throw unauthenticated and deny the connection</returns>
        public virtual Task<bool> AuthenticateRequestAsync(IOwinRequest request)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Fires after the websocket has been opened with the client
        /// </summary>
        public virtual void OnOpen()
        {
        }
        
        /// <summary>
        /// Fires after the websocket has been opened with the client and after OnOpen
        /// </summary>
        public virtual Task OnOpenAsync()
        {
            return Task.Delay(0);
        }

        /// <summary>
        /// Fires when data is received from the client
        /// </summary>
        /// <param name="message">Data that was received</param>
        /// <param name="type">Message type of the data</param>
        public virtual Task OnMessageReceived(ArraySegment<byte> message, WebSocketMessageType type)
        {
            return Task.Delay(0);
        }

        /// <summary>
        /// Fires with the connection with the client has closed
        /// </summary>
        public virtual void OnClose(WebSocketCloseStatus? closeStatus, string closeStatusDescription)
        {
        }

        /// <summary>
        /// Fires with the connection with the client has closed and after OnClose
        /// </summary>
        public virtual Task OnCloseAsync(WebSocketCloseStatus? closeStatus, string closeStatusDescription)
        {
            return Task.Delay(0);
        }

        /// <summary>
        /// Fires when an exception occurs in the message reading loop
        /// </summary>
        /// <param name="error">Error that occured</param>
        public virtual void OnReceiveError(Exception error)
        {
        }

        /// <summary>
        /// Receive one entire message from the web socket
        /// </summary>


        internal async Task AcceptSocketAsync(IOwinContext context, IDictionary<string, string> argumentMatches)
        {
            var accept = context.Get<Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>>("websocket.Accept");
            if (accept == null)
            {
                // Bad Request
                context.Response.StatusCode = 400;
                context.Response.Write("Not a valid websocket request");
                return;
            }

            Arguments = new Dictionary<string, string>(argumentMatches);

            var responseBuffering = context.Environment.Get<Action>("server.DisableResponseBuffering");
            if (responseBuffering != null)
                responseBuffering();

            var responseCompression = context.Environment.Get<Action>("systemweb.DisableResponseCompression");
            if (responseCompression != null)
                responseCompression();

            context.Response.Headers.Set("X-Content-Type-Options", "nosniff");

            Context = context;

            if (AuthenticateRequest(context.Request))
            {
                var authorized = await AuthenticateRequestAsync(context.Request);
                if (authorized)
                {
                    //user was authorized so accept the socket
                    accept(null, RunWebSocket);
                    return;

                }
            }

            //see if user was forbidden or unauthorized from previous authenticate request failure
            if (context.Request.User != null && context.Request.User.Identity.IsAuthenticated)
            {
                context.Response.StatusCode = 403;
            }
            else
            {
                context.Response.StatusCode = 401;
            }
        }

        private async Task RunWebSocket(IDictionary<string, object> websocketContext)
        {
            object value;
            if (websocketContext.TryGetValue(typeof (WebSocketContext).FullName, out value))
            {
                mWebSocket = new NetWebSocket(((WebSocketContext) value).WebSocket);
            }
            else
            {
                mWebSocket = new OwinWebSocket(websocketContext);
            }

            OnOpen();
            await OnOpenAsync();

            var buffer = new byte[MaxMessageSize];
            Tuple<ArraySegment<byte>, WebSocketMessageType> received = null;

            do
            {
                try
                {
                    received = await mWebSocket.ReceiveMessage(buffer, mCancellToken.Token);
                    if (received.Item1.Count > 0)
                        await OnMessageReceived(received.Item1, received.Item2);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (OperationCanceledException oce)
                {
                    if (!mCancellToken.IsCancellationRequested)
                    {
                        OnReceiveError(oce);
                    }
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (IsFatalSocketException(ex))
                    {
                        OnReceiveError(ex);
                    }
                    break;
                }
            }
            while (received.Item2 != WebSocketMessageType.Close);

            try
            {
                await mWebSocket.Close(WebSocketCloseStatus.NormalClosure, string.Empty, mCancellToken.Token);
            }
            catch
            { //Ignore
            }

            if(!mCancellToken.IsCancellationRequested)
                mCancellToken.Cancel();

            OnClose(mWebSocket.CloseStatus, mWebSocket.CloseStatusDescription);
            await OnCloseAsync(mWebSocket.CloseStatus, mWebSocket.CloseStatusDescription);
        }

        internal static bool IsFatalSocketException(Exception ex)
        {
            // If this exception is due to the underlying TCP connection going away, treat as a normal close
            // rather than a fatal exception.
            var ce = ex as COMException;
            if (ce != null)
            {
                switch ((uint)ce.ErrorCode)
                {
                    case 0x800703e3:
                    case 0x800704cd:
                    case 0x80070026:
                        return false;
                }
            }

            // unknown exception; treat as fatal
            return true;
        }
    }
}