using Owin;
using Owin.WebSocket;
using Owin.WebSocket.Extensions;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using static LanguageExt.Prelude;
using static LanguageExt.Process;
using LanguageExt;
using LanguageExt.Client;
using Newtonsoft.Json;

namespace LanguageExt
{
    public static class ProcessOwin
    {
        public static Unit initialise(IAppBuilder app, string route = "/process-sys" )
        {
            app.MapWebSocketRoute<ProcessSysWebSocket>(route ?? "/process-sys");
            return unit;
        }
    }

    public class ProcessSysWebSocket : WebSocketConnection
    {
        void Tell(ClientMessageDTO message)
        {
            try
            {
                SendText(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)), true);
            }
            catch(Exception e)
            {
                // TODO: logErr(e);
                Console.WriteLine(e.Message);
            }
        }

        public override async Task OnMessageReceived(ArraySegment<byte> message, WebSocketMessageType type) =>
            await Req.Parse(Encoding.UTF8.GetString(message.Array, message.Offset, message.Count), ProcessHub.Connections).MatchAsync(
                Right: async msg =>
                {
                    switch (msg)
                    {
                        case TellReq req:
                        {
                            var to = FixRootName(req.To);

                            if (ProcessHub.RouteValidator(to))
                            {
                                var sender = req.Sender.IsValid
                                    ? Root(to.System)["js"][(string)req.Id].Append(req.Sender)
                                    : ProcessId.NoSender;

                                tell(to, req.Message, sender);
                            }
                            await SendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"tellr\",\"id\":\"{req.Id}\"}}"), true);
                            return unit;
                        }

                        case AskReq req:
                        {
                            try
                            {
                                var to = FixRootName(req.To);

                                if (ProcessHub.RouteValidator(to))
                                {
                                    var sender = req.Sender.IsValid
                                        ? Root(to.System)["js"][(string)req.Id].Append(req.Sender)
                                        : ProcessId.NoSender;

                                    var result = await askAsync<object>(to, req.Message, req.Sender);
                                    await SendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"askr\",\"id\":\"{req.Id}\",\"mid\":\"{req.MessageId}\",\"done\":{JsonConvert.SerializeObject(result)}}}"), true);
                                }
                                else
                                {
                                    await SendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"askr\",\"id\":\"{req.Id}\",\"mid\":\"{req.MessageId}\",\"fail\":\"Invalid route\"}}"), true);
                                }
                            }
                            catch
                            {
                                await SendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"askr\",\"id\":\"{req.Id}\",\"mid\":\"{req.MessageId}\",\"fail\":\"Error\"}}"), true);
                                // TODO:
                                //logSysErr(e);
                            }
                            return unit;
                        }

                        case ConnectReq req:
                            var conn = ProcessHub.OpenConnection(Tell);
                            await SendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"conn\",\"id\":\"{conn}\"}}"), true);
                            return unit;

                        case DisconnectReq req:
                            ProcessHub.CloseConnection(req.Id);
                            await SendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"disc\",\"id\":\"{req.Id}\"}}"), true);
                            return unit;

                        case SubscribeReq req:
                            ProcessHub.Subscribe(req.Id, req.Publisher, req.Subscriber);
                            await SendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"subr\",\"id\":\"{req.Id}\",\"pub\":\"{req.Publisher}\",\"sub\":\"{req.Subscriber}\"}}"), true);
                            return unit;

                        case UnSubscribeReq req:
                            ProcessHub.UnSubscribe(req.Id, req.Publisher, req.Subscriber);
                            await SendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"usubr\",\"id\":\"{req.Id}\",\"pub\":\"{req.Publisher}\",\"sub\":\"{req.Subscriber}\"}}"), true);
                            return unit;

                        case PingReq req:
                            ProcessHub.TouchConnection(req.Id);
                            await SendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"pong\",\"id\":\"{req.Id}\"}}"), true);
                            return unit;

                        default:
                            // TODO:
                            //logSysErr($"Unknown message type in switch: {msg?.GetType()?.FullName}");
                            return unit;
                    }
                },
                Left: err => /*TODO: logUserErr(err)*/ unit);

        static ProcessId FixRootName(ProcessId pid) =>
            pid.Take(1).Name.Value == "root"
                ? Root().Append(pid.Skip(1))
                : pid;

        public override void OnOpen()
        {
        }

        public override void OnClose(WebSocketCloseStatus? closeStatus, string closeStatusDescription)
        {
        }
    }
}
