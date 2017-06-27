using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace Echo
{
    class ActorRequest : UserControlMessage
    {
        public readonly object Message;
        public readonly ProcessId To;
        public readonly ProcessId ReplyTo;
        public readonly long RequestId;

        public override Type MessageType => Type.User;
        public override TagSpec Tag      => TagSpec.UserAsk;

        [JsonConstructor]
        public ActorRequest(object message, ProcessId to, ProcessId replyTo, long requestId)
        {
            Message = message;
            To = to;
            ReplyTo = replyTo;
            RequestId = requestId;
        }

        public ActorRequest SetSystem(SystemName sys) =>
            new ActorRequest(Message, To.SetSystem(sys), ReplyTo.SetSystem(sys), RequestId);

        public override string ToString() =>
            $"ActorRequest from {ReplyTo} to {To}: {Message}";
    }

    internal class ActorResponse : UserControlMessage
    {
        public readonly ProcessId ReplyTo;
        public readonly object Message;
        public readonly ProcessId ReplyFrom;
        public readonly long RequestId;
        public readonly bool IsFaulted;
        public readonly string ReplyType;

        public override Type MessageType => Type.User;
        public override TagSpec Tag      => TagSpec.UserReply;

        [JsonConstructor]
        public ActorResponse(object message, ProcessId replyTo, ProcessId replyFrom, long requestId, string replyType, bool isFaulted = false)
        {
            Message = message;
            ReplyTo = replyTo;
            ReplyFrom = replyFrom;
            RequestId = requestId;
            IsFaulted = isFaulted;
            ReplyType = replyType;
        }

        public ActorResponse SetSystem(SystemName sys) =>
            new ActorResponse(Message, ReplyTo.SetSystem(sys), ReplyFrom.SetSystem(sys), RequestId, ReplyType, IsFaulted);

        public override string ToString() =>
            $"ActorResponse to {ReplyTo} from {ReplyFrom}: {Message}";
    }
}
