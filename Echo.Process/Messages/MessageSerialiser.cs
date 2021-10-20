using LanguageExt.UnitsOfMeasure;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Echo
{
    internal static class MessageSerialiser
    {
        static RemoteMessageDTO FixupPathsSystemPrefix(RemoteMessageDTO dto, SystemName system)
        {
            if (dto == null) return null;

            // Fix up the paths so we know what system they belong to.
            dto.ReplyTo = String.IsNullOrEmpty(dto.ReplyTo) || dto.ReplyTo.StartsWith("//") ? dto.ReplyTo : $"//{system}{dto.ReplyTo}";
            dto.Sender = String.IsNullOrEmpty(dto.Sender) || dto.Sender.StartsWith("//") ? dto.Sender : $"//{system}{dto.Sender}";
            dto.To = String.IsNullOrEmpty(dto.To) || dto.To.StartsWith("//") ? dto.To : $"//{system}{dto.To}";
            return dto;
        }

        public static Message DeserialiseMsg(RemoteMessageDTO msg, ProcessId actorId)
        {
            var sys = actorId.System;
            msg = FixupPathsSystemPrefix(msg, sys);

            var sender = String.IsNullOrEmpty(msg.Sender) ? ProcessId.NoSender : new ProcessId(msg.Sender);
            var replyTo = String.IsNullOrEmpty(msg.ReplyTo) ? ProcessId.NoSender : new ProcessId(msg.ReplyTo);

            Message rmsg = (Message.TagSpec) msg.Tag switch
                           {
                               Message.TagSpec.UserReply =>
                                   DeserialiseMsgContent(msg) switch
                                   {
                                       null        => throw new Exception($"Failed to deserialise message: {msg.Tag}"),
                                       var content => new ActorResponse(content, actorId, sender, msg.RequestId, content.GetType().AssemblyQualifiedName, msg.Exception == "RESPERR")
                                   },
                               Message.TagSpec.UserAsk         => new ActorRequest(DeserialiseMsgContent(msg), actorId, replyTo.SetSystem(sys), msg.RequestId),
                               Message.TagSpec.User            => new UserMessage(DeserialiseMsgContent(msg), sender.SetSystem(sys), replyTo.SetSystem(sys)),
                               Message.TagSpec.UserTerminated  => ((TerminatedMessage) DeserialiseMsgContent(msg)).SetSystem(sys),
                               Message.TagSpec.GetChildren     => UserControlMessage.GetChildren,
                               Message.TagSpec.StartupProcess  => (StartupProcessMessage) DeserialiseMsgContent(msg),
                               Message.TagSpec.ShutdownProcess => (ShutdownProcessMessage) DeserialiseMsgContent(msg),
                               Message.TagSpec.Restart         => SystemMessage.Restart,
                               Message.TagSpec.Pause           => SystemMessage.Pause,
                               Message.TagSpec.Unpause         => SystemMessage.Unpause,
                               Message.TagSpec.DispatchWatch   => (SystemDispatchWatchMessage) DeserialiseMsgContent(msg),
                               Message.TagSpec.DispatchUnWatch => (SystemDispatchUnWatchMessage) DeserialiseMsgContent(msg),
                               Message.TagSpec.Watch           => (SystemAddWatcherMessage) DeserialiseMsgContent(msg),
                               Message.TagSpec.UnWatch         => (SystemRemoveWatcherMessage) DeserialiseMsgContent(msg),
                               _                               => throw new Exception($"Unknown Message Tag: {msg.Tag}")
                           };

            rmsg.ConversationId = msg.ConversationId;
            rmsg.SessionId      = msg.SessionId; 
            return rmsg;
        }

        private static object DeserialiseMsgContent(RemoteMessageDTO msg)
        {
            object content = null;

            if (msg.Content == null)
            {
                throw new Exception($"Message content is null from {msg.Sender}");
            }
            else
            {
                var contentType = Type.GetType(msg.ContentType);
                if (contentType == null)
                {
                    throw new Exception($"Can't resolve type: {msg.ContentType}");
                }

                content = Deserialise.Object(msg.Content, contentType);
            }

            return content;
        }
    }
}
