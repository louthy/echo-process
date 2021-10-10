using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading;
using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;

namespace Echo
{
    /// <summary>
    /// The ask process helps turn tells into request/responses, by maintaining a list of requests that can be paired
    /// up to responses when they return.
    /// </summary>
    internal static class AskActor
    {
        public const int Actors = 32;

        /// <summary>
        /// Ask process setup
        /// </summary>
        public static AskActorState Setup()
        {
            TellFlushExpired();
            return AskActorState.Empty;
        }

        /// <summary>
       /// Ask inbox, receives requests and responses
       /// </summary>
        public static AskActorState Inbox(AskActorState state, object msg) =>
            msg switch
            {
                AskActorReq nreq   => ProcessRequest(state, nreq),
                ActorResponse nres => ProcessResponse(state, nres),
                AskActorFlush _    => state.FlushExpired(),
                _                  => state
            };

        /// <summary>
       /// Process an ask request
       /// </summary>
       /// <remarks>
       /// We package up the request with a request-ID that we generate and post it off (via a tell) to its destination
       /// We also spoof the return address to be this process, which means the response comes back to us
       /// </remarks>
        static AskActorState ProcessRequest(AskActorState state, AskActorReq req)
        {
            try
            {
                ActorContext.System(req.To).Ask(req.To, new ActorRequest(req.Message, req.To, Self, req.RequestId), Self);
                return state.AddRequest(req);
            }
            catch(Exception e)
            {
                req.Complete(new AskActorRes(new ProcessException($"Process issue: {e.Message}", req.To.Path, req.ReplyTo.Path, e), req.ReplyType));
                logUserErr(e.Message);
                return state;
            }
        }

        /// <summary>
        /// Process an ask response
        /// </summary>
        static AskActorState ProcessResponse(AskActorState state, ActorResponse res) =>
            state.Requests.Find(res.RequestId).Case switch
            {
                AskActorReq req => ProcessResponse(state, req, res),
                _               => Warn(state, $"Request ID doesn't exist: {res.RequestId}. {res}")
            };

        /// <summary>
        /// Process an ask response
        /// </summary>
        static AskActorState ProcessResponse(AskActorState state, AskActorReq req, ActorResponse res)
        {
            try
            {
                return res.IsFaulted
                           ? ProcessResponseError(state, req, res).RemoveRequest(res.RequestId)
                           : ProcessResponseSuccess(state, req, res).RemoveRequest(res.RequestId);
            }
            catch(Exception e)
            {
                req.Complete(new AskActorRes(new ProcessException($"Process issue: {e.Message}", req.To.Path, req.ReplyTo.Path, e), req.ReplyType));
                logSysErr(e);
                return state.RemoveRequest(res.RequestId);
            }
        }

        /// <summary>
        /// Process an ask response
        /// </summary>
        static AskActorState ProcessResponseSuccess(AskActorState state, AskActorReq req, ActorResponse res)
        {
            req.Complete(new AskActorRes(res.Message, req.ReplyType));
            return state;
        }

        /// <summary>
        /// Process an ask response
        /// </summary>
        static AskActorState ProcessResponseError(AskActorState state, AskActorReq req, ActorResponse res)
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
            return state;
        }
        
        /// <summary>
        /// Clear out any requests over a minute old
        /// </summary>
        static AskActorState FlushRequests(AskActorState state)
        {
            TellFlushExpired();
            return state.FlushExpired();
        }

        /// <summary>
        /// Announce that we need to flush expired requests every minute
        /// </summary>
        static Unit TellFlushExpired() =>
            tellSelf(AskActorFlush.Default, Echo.Schedule.Ephemeral(10 * minute, "flush"));

        static AskActorState Warn(AskActorState state, string msg)
        {
            logWarn(msg);
            return state;
        }
    }

    /// <summary>
    /// State for the ask process
    /// </summary>
    internal record AskActorState(HashMap<long, AskActorReq> Requests)
    {
        public static readonly AskActorState Empty = new AskActorState(HashMap<long, AskActorReq>());

        public AskActorState RemoveRequest(long request) =>
            this with {Requests = Requests.Remove(request)};
        
        public AskActorState AddRequest(AskActorReq req) =>
            this with {Requests = Requests.Add(req.RequestId, req)};

        public AskActorState FlushExpired()
        {
            var cutOff = DateTime.UtcNow.AddMinutes(-1).Ticks;
            return this with {Requests = Requests.Filter(r => r.Created < cutOff)};
        }
    }

    internal class AskActorReq
    {
        static long RequestIndex = 1L;  
        
        public readonly long RequestId;
        public readonly long Created;
        public readonly object Message;
        public readonly Action<AskActorRes> Complete;
        public readonly ProcessId To;
        public readonly ProcessId ReplyTo;
        public readonly Type ReplyType;

        public AskActorReq(long created, object msg, Action<AskActorRes> complete, ProcessId to, ProcessId replyTo, Type replyType)
        {
            RequestId = Interlocked.Increment(ref RequestIndex);
            Created   = created;
            Complete  = complete;
            Message   = msg;
            To        = to;
            ReplyTo   = replyTo;
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

    internal record AskActorFlush
    {
        public static readonly AskActorFlush Default = new();
    }
}
