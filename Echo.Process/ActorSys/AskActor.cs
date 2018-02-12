using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading;
using static LanguageExt.Prelude;
using static Echo.Process;

namespace Echo
{
    internal static class AskActor
    {
        const int responseActors = 20;

        public static (long RequestId, Dictionary<long, AskActorReq> Requests) Inbox((long RequestId, Dictionary<long, AskActorReq> Requests) state, object msg)
        {
            var reqId = state.Item1;
            var dict = state.Item2;

            if (msg is AskActorReq)
            {
                reqId++;

                var req = (AskActorReq)msg;
                ActorContext.System(req.To).Ask(req.To, new ActorRequest(req.Message, req.To, Self, reqId), Self);
                dict.Add(reqId, req);
            }
            else
            {
                var res = (ActorResponse)msg;
                if (dict.ContainsKey(res.RequestId))
                {
                    var req = dict[res.RequestId];
                    try
                    {
                        if (res.IsFaulted)
                        {
                            Exception ex = null;

                            // Let's be reeeally safe here and do everything we can to get some valid information out of
                            // the response to report to the process doing the 'ask'.  
                            try
                            {
                                var msgtype = req.ReplyType;
                                if (msgtype == res.Message.GetType() && typeof(Exception).GetTypeInfo().IsAssignableFrom(msgtype.GetTypeInfo()))
                                {
                                    // Type is fine, just return it as an error
                                    ex = (Exception)res.Message;
                                }
                                else
                                {
                                    if (res.Message is string)
                                    {
                                        ex = (Exception)Deserialise.Object(res.Message.ToString(), msgtype);
                                    }
                                    else
                                    {
                                        ex = (Exception)Deserialise.Object(JsonConvert.SerializeObject(res.Message), msgtype);
                                    }
                                }
                            }
                            catch
                            {
                                ex = new Exception(res.Message?.ToString() ?? $"An unknown error was thrown by {req.To}");
                            }

                            req.Complete(new AskActorRes(new ProcessException($"Process issue: {ex.Message}", req.To.Path, req.ReplyTo.Path, ex), req.ReplyType));
                        }
                        else
                        {
                            req.Complete(new AskActorRes(res.Message, req.ReplyType));
                        }
                    }
                    catch (Exception e)
                    {
                        req.Complete(new AskActorRes(new ProcessException($"Process issue: {e.Message}", req.To.Path, req.ReplyTo.Path, e), req.ReplyType));
                        logSysErr(e);
                    }
                    finally
                    {
                        dict.Remove(res.RequestId);
                    }
                }
                else
                {
                    logWarn($"Request ID doesn't exist: {res.RequestId}");
                }
            }

            return (reqId, dict);
        }

        public static (long, Dictionary<long, AskActorReq>) Setup() =>
            (1L, new Dictionary<long, AskActorReq>());
    }

    internal class AskActorReq
    {
        public readonly object Message;
        public readonly Action<AskActorRes> Complete;
        public readonly ProcessId To;
        public readonly ProcessId ReplyTo;
        public readonly Type ReplyType;

        public AskActorReq(object msg, Action<AskActorRes> complete, ProcessId to, ProcessId replyTo, Type replyType)
        {
            Complete = complete;
            Message = msg;
            To = to;
            ReplyTo = replyTo;
            ReplyType = replyType;
        }

        public override string ToString() =>
            $"Ask request from: {ReplyTo} to: {To} msg: {Message}";
    }

    internal class AskActorRes
    {
        public bool IsFaulted => Exception != null;
        public readonly Exception Exception;
        public readonly object Response;
        public readonly Type AskedResponseType;

        public AskActorRes(Exception exception, Type askedResponseType)
        {
            Exception = exception;
            AskedResponseType = askedResponseType;
        }
        public AskActorRes(object response, Type askedResponseType)
        {
            Response = response;
            AskedResponseType = askedResponseType;
        }
    }
}
