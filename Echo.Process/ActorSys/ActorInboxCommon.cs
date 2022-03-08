using System;
using System.Threading.Tasks;
using static LanguageExt.Prelude;
using static Echo.Process;
using LanguageExt;

namespace Echo
{
    static class ActorInboxCommon
    {
        public static async ValueTask<InboxDirective> SystemMessageInbox<S,T>(Actor<S,T> actor, IActorInbox inbox, SystemMessage msg, ActorItem parent)
        {
            var session = msg.SessionId == null
                ? None
                : Some(new SessionId(msg.SessionId));

            return await ActorContext.System(actor.Id).WithContext(new ActorItem(actor,inbox,actor.Flags), parent, ProcessId.NoSender, null, msg, session, async () =>
            {
                switch (msg.Tag)
                {
                    case Message.TagSpec.Restart:
                        await actor.Restart(inbox.IsPaused).ConfigureAwait(false);
                        return InboxDirective.Default;

                    case Message.TagSpec.LinkChild:
                        var lc = (SystemLinkChildMessage)msg;
                        actor.LinkChild(lc.Child);
                        return InboxDirective.Default;

                    case Message.TagSpec.UnlinkChild:
                        var ulc = ((SystemUnLinkChildMessage)msg).SetSystem(actor.Id.System);
                        actor.UnlinkChild(ulc.Child);
                        return InboxDirective.Default;

                    case Message.TagSpec.ChildFaulted:
                        var cf = ((SystemChildFaultedMessage)msg).SetSystem(actor.Id.System);
                        return await actor.ChildFaulted(cf.Child, cf.Sender, cf.Exception, cf.Message).ConfigureAwait(false);

                    case Message.TagSpec.StartupProcess:
                        // get feedback whether startup will somehow trigger Unpause itself (i.e. error => strategy => restart)
                        var startupProcess = (StartupProcessMessage)msg;
                        var inboxDirective = await actor.Startup().ConfigureAwait(false); 
                        if (startupProcess.UnpauseAfterStartup && !inboxDirective.HasFlag(InboxDirective.Pause))
                        {
                            inbox.Unpause();
                        }
                        return InboxDirective.Default;

                    case Message.TagSpec.ShutdownProcess:
                        var shutdownProcess = (ShutdownProcessMessage)msg;
                        await actor.Shutdown(shutdownProcess.MaintainState).ConfigureAwait(false);
                        return InboxDirective.Default;

                    case Message.TagSpec.Unpause:
                        inbox.Unpause();
                        actor.UnPause();
                        return InboxDirective.Default;
                        
                    case Message.TagSpec.Pause:
                        inbox.Pause();
                        actor.Pause();
                        return InboxDirective.Default; // do not return InboxDirective.Pause because system queue should never pause

                    case Message.TagSpec.Watch:
                        var awm = (SystemAddWatcherMessage)msg;
                        actor.AddWatcher(awm.Id);
                        return InboxDirective.Default;

                    case Message.TagSpec.UnWatch:
                        var rwm = (SystemRemoveWatcherMessage)msg;
                        actor.RemoveWatcher(rwm.Id);
                        return InboxDirective.Default;

                    case Message.TagSpec.DispatchWatch:
                        var dwm = (SystemDispatchWatchMessage)msg;
                        actor.DispatchWatch(dwm.Id);
                        return InboxDirective.Default;

                    case Message.TagSpec.DispatchUnWatch:
                        var duwm = (SystemDispatchUnWatchMessage)msg;
                        actor.DispatchUnWatch(duwm.Id);
                        return InboxDirective.Default;
                    
                    default:
                        return InboxDirective.Default;
                }
            }).ConfigureAwait(false);
        }

        public static async ValueTask<InboxDirective> UserMessageInbox<S, T>(Actor<S, T> actor, IActorInbox inbox, UserControlMessage msg, ActorItem parent)
        {
            var session = msg.SessionId == null 
                ? None 
                : Some(new SessionId(msg.SessionId));

            switch (msg.Tag)
            {
                case Message.TagSpec.UserAsk:
                    var rmsg = ((ActorRequest)msg).SetSystem(actor.Id.System);
                    return await ActorContext.System(actor.Id)
                                             .WithContext(new ActorItem(actor, inbox, actor.Flags), parent, rmsg.ReplyTo, rmsg, msg, session, () => actor.ProcessAsk(rmsg))
                                             .ConfigureAwait(false);

                case Message.TagSpec.UserReply:
                    var urmsg = ((ActorResponse)msg).SetSystem(actor.Id.System);
                    await ActorContext.System(actor.Id).WithContext(new ActorItem(actor, inbox, actor.Flags), parent, urmsg.ReplyFrom, null, msg, session, () => actor.ProcessResponse(urmsg));
                    return InboxDirective.Default;

                case Message.TagSpec.UserTerminated:
                    var utmsg = ((TerminatedMessage)msg).SetSystem(actor.Id.System);
                    return await ActorContext.System(actor.Id)
                                             .WithContext(new ActorItem(actor, inbox, actor.Flags), parent, utmsg.Id, null, msg, session, () => actor.ProcessTerminated(utmsg.Id))
                                             .ConfigureAwait(false);

                case Message.TagSpec.User:
                    var umsg = ((UserMessage)msg).SetSystem(actor.Id.System);
                    return await ActorContext.System(actor.Id)
                                             .WithContext(new ActorItem(actor, inbox, actor.Flags), parent, umsg.Sender, null, msg, session, () => actor.ProcessMessage(umsg.Content))
                                             .ConfigureAwait(false);

                case Message.TagSpec.ShutdownProcess:
                    kill(actor.Id);
                    return InboxDirective.Default;
                
                default:
                    return InboxDirective.Default;
            }
        }

        public static Option<UserControlMessage> PreProcessMessage<T>(ProcessId sender, ProcessId self, object message, Option<SessionId> sessionId)
        {
            if (message == null)
            {
                var emsg = $"Message is null for tell (expected {typeof(T)})";
                tell(ActorContext.System(self).DeadLetters, DeadLetter.create(sender, self, emsg));
                return None;
            }

            UserControlMessage rmsg = null;

            if (message is ActorRequest req)
            {
                if (!(req.Message is T) && !(req.Message is Message))
                {
                    var emsg = $"Invalid message type for ask (expected {typeof(T)})";
                    tell(ActorContext.System(self).DeadLetters, DeadLetter.create(sender, self, emsg, req));

                    ActorContext.System(self).Tell(
                        sender,
                        new ActorResponse(new Exception($"Invalid message type for ask (expected {typeof(T)})"),
                            sender,
                            self,
                            req.RequestId,
                            typeof(Exception).AssemblyQualifiedName,
                            true
                        ),
                        Schedule.Immediate,
                        self
                    );

                    return None;
                }
                rmsg = req;
            }
            else
            {
                rmsg = new UserMessage(message, sender, sender);
            }

            if(rmsg.SessionId == null && sessionId.IsSome)
            {
                rmsg.SessionId = sessionId.Map(x => x.Value).IfNoneUnsafe((string)null);
            }
            return Optional(rmsg);
        }

        public static Option<(RemoteMessageDTO, Message)> GetNextMessage(ICluster cluster, ProcessId self, string key)
        {
            if (cluster == null) return None;
            Message msg = null;
            RemoteMessageDTO dto = null;

            dto = null;
            do
            {
                dto = cluster.Peek<RemoteMessageDTO>(key);
                if (dto == null)
                {
                    // Queue is empty
                    return None; 
                }
                if (dto.Tag == 0 && dto.Type == 0)
                {
                    // Message is bad
                    cluster.Dequeue<RemoteMessageDTO>(key);
                    tell(ActorContext.System(self).DeadLetters, DeadLetter.create(dto.Sender, self, null, "Failed to deserialise message: ", dto));
                    if (cluster.QueueLength(key) == 0) return None;
                }
            }
            while (dto == null || dto.Tag == 0 || dto.Type == 0);

            try
            {
                msg = MessageSerialiser.DeserialiseMsg(dto, self);
                msg.SessionId = dto.SessionId;
            }
            catch (Exception e)
            {
                // Message can't be deserialised
                cluster.Dequeue<RemoteMessageDTO>(key);
                tell(ActorContext.System(self).DeadLetters, DeadLetter.create(dto.Sender, self, e, "Failed to deserialise message: ", msg));
                return None;
            }

            return Some((dto, msg));
        }

        public static string ClusterKey(ProcessId pid) =>
            pid.Path;

        public static string ClusterSettingsKey(ProcessId pid) =>
            ClusterKey(pid) + "@settings";

        public static string ClusterInboxKey(ProcessId pid, string type) =>
            ClusterKey(pid) + "-" + type + "-inbox";

        public static string ClusterUserInboxKey(ProcessId pid) =>
            ClusterInboxKey(pid, "user");

        public static string ClusterSystemInboxKey(ProcessId pid) =>
            ClusterInboxKey(pid, "system");

        public static string ClusterInboxNotifyKey(ProcessId pid, string type) =>
            ClusterInboxKey(pid, type) + "-notify";

        public static string ClusterUserInboxNotifyKey(ProcessId pid) =>
            ClusterInboxNotifyKey(pid, "user");

        public static string ClusterSystemInboxNotifyKey(ProcessId pid) =>
            ClusterInboxNotifyKey(pid, "system");

        public static string ClusterMetaDataKey(ProcessId pid) =>
            ClusterKey(pid) + "-metadata";

        public static string ClusterPubSubKey(ProcessId pid) =>
            ClusterKey(pid) + "-pubsub";

        public static string ClusterStatePubSubKey(ProcessId pid) =>
            ClusterKey(pid) + "-state-pubsub";

        public static string ClusterScheduleKey(ProcessId pid) =>
            $"/__schedule{ClusterKey(pid)}-user-schedule";

        public static string ClusterScheduleNotifyKey(ProcessId pid) =>
            ClusterScheduleKey(pid) + "-notify";
    }
}
