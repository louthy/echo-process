using System;
using System.Net;
using System.Text;
using LanguageExt;
using Newtonsoft.Json;
using static Echo.Process;
using System.Threading.Tasks;
using static LanguageExt.Prelude;

namespace Echo.Client
{
    public static class ClientMessaging
    {
        public static async Task<Unit> Write(Req msg, IPAddress remoteIpAddress, Func<byte[], bool, Task> sendText)
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
                    await sendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"tellr\",\"id\":\"{req.Id}\"}}"), true).ConfigureAwait(false);
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

                            var   result = await askAsync<object>(to, req.Message, req.Sender).ConfigureAwait(false);
                            await sendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"askr\",\"id\":\"{req.Id}\",\"mid\":\"{req.MessageId}\",\"done\":{JsonConvert.SerializeObject(result)}}}"), true).ConfigureAwait(false);
                        }
                        else
                        {
                            await sendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"askr\",\"id\":\"{req.Id}\",\"mid\":\"{req.MessageId}\",\"fail\":\"Invalid route\"}}"), true).ConfigureAwait(false);
                        }
                    }
                    catch(Exception e)
                    {
                        await sendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"askr\",\"id\":\"{req.Id}\",\"mid\":\"{req.MessageId}\",\"fail\":\"Error\"}}"), true).ConfigureAwait(false);
                        logErr(e);
                    }
                    return unit;
                }

                case ConnectReq req:
                    var   conn = ProcessHub.OpenConnection(
                        remoteIpAddress.ToString(), 
                        message => {
                            try
                            {
                                sendText(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)), true).ConfigureAwait(false);
                            }
                            catch (Exception e)
                            {
                                logErr(e);
                            }
                        });
                    await sendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"conn\",\"id\":\"{conn}\"}}"), true).ConfigureAwait(false);
                    return unit;

                case DisconnectReq req:
                    ProcessHub.CloseConnection(req.Id);
                    await sendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"disc\",\"id\":\"{req.Id}\"}}"), true).ConfigureAwait(false);
                    return unit;

                case SubscribeReq req:
                    ProcessHub.Subscribe(req.Id, req.Publisher, req.Subscriber);
                    await sendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"subr\",\"id\":\"{req.Id}\",\"pub\":\"{req.Publisher}\",\"sub\":\"{req.Subscriber}\"}}"), true).ConfigureAwait(false);
                    return unit;

                case UnSubscribeReq req:
                    ProcessHub.UnSubscribe(req.Id, req.Publisher, req.Subscriber);
                    await sendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"usubr\",\"id\":\"{req.Id}\",\"pub\":\"{req.Publisher}\",\"sub\":\"{req.Subscriber}\"}}"), true).ConfigureAwait(false);
                    return unit;

                case PingReq req:
                    await sendText(Encoding.UTF8.GetBytes($"{{\"tag\":\"pong\",\"id\":\"{req.Id}\",\"status\":\"{ProcessHub.TouchConnection(req.Id)}\"}}"), true).ConfigureAwait(false);
                    return unit;

                default:
                    logErr($"Unknown message type in switch: {msg?.GetType()?.FullName}");
                    return unit;
            }
        }
        
        static ProcessId FixRootName(ProcessId pid) =>
            pid.Take(1).Name.Value == "root"
                ? Root().Append(pid.Skip(1))
                : pid;
    }
}