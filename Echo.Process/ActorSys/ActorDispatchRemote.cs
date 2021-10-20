using System;
using System.Linq;
using System.Reflection;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using static LanguageExt.Prelude;
using static Echo.Process;
using System.Collections.Generic;
using LanguageExt;

namespace Echo
{
    class ActorDispatchRemote : IActorDispatch
    {
        public readonly ProcessId ProcessId;
        public readonly ICluster Cluster;
        public readonly Option<SessionId> SessionId;
        public readonly long ConversationId;
        public readonly Ping Pinger;

        public ActorDispatchRemote(Ping ping, ProcessId pid, ICluster cluster, Option<SessionId> sessionId)
        {
            Pinger         = ping;
            ProcessId      = pid;
            Cluster        = cluster;
            SessionId      = sessionId;
            ConversationId = ActorContext.NextOrCurrentConversationId();
        }

        public HashMap<string, ProcessId> GetChildren() =>
            ask<HashMap<string, ProcessId>>(ProcessId, UserControlMessage.GetChildren);

        public IObservable<T> Observe<T>() =>
            Cluster.SubscribeToChannel<T>(ActorInboxCommon.ClusterPubSubKey(ProcessId));

        public IObservable<T> ObserveState<T>() =>
            Cluster.SubscribeToChannel<T>(ActorInboxCommon.ClusterStatePubSubKey(ProcessId));

        public Either<string, bool> HasStateTypeOf<T>()
        {
            if (Cluster.Exists(ActorInboxCommon.ClusterMetaDataKey(ProcessId)))
            {
                var meta = Cluster.GetValue<ProcessMetaData>(ActorInboxCommon.ClusterMetaDataKey(ProcessId));
                if(meta == null)
                {
                    return true;
                }

                return TypeHelper.HasStateTypeOf(typeof(T), meta.StateTypeInterfaces);
            }
            else
            {
                return true;
            }
        }

        public Either<string, bool> CanAccept<T>()
        {
            if (Cluster.Exists(ActorInboxCommon.ClusterMetaDataKey(ProcessId)))
            {
                var meta = Cluster.GetValue<ProcessMetaData>(ActorInboxCommon.ClusterMetaDataKey(ProcessId));
                return meta == null
                    ? true
                    : TypeHelper.IsMessageValidForProcess(typeof(T), meta.MsgTypeNames).Map(_ => true);
            }
            else
            {
                return true;
            }
        }

        void ValidateMessageType(object message, ProcessId sender)
        {
            if (Cluster.Exists(ActorInboxCommon.ClusterMetaDataKey(ProcessId)))
            {
                var meta = Cluster.GetValue<ProcessMetaData>(ActorInboxCommon.ClusterMetaDataKey(ProcessId));
                if( meta == null )
                {
                    return;
                }

                TypeHelper.IsMessageValidForProcess(message, meta.MsgTypeNames).IfLeft((string err) =>
                {
                    throw new ProcessException($"{err} for Process ({ProcessId}).", ProcessId.Path, sender.Path, null);
                });
            }
        }

        public Unit Tell(object message, Schedule schedule, ProcessId sender, Message.TagSpec tag) =>
            Tell(message, schedule, sender, "user", Message.Type.User, tag);

        public Unit TellUserControl(UserControlMessage message, ProcessId sender)
        {
            message.ConversationId = ConversationId;
            message.SessionId      = SessionId.Map(static x => x.Value).IfNoneUnsafe(message.SessionId);
            return Tell(message, Schedule.Immediate, sender, "user", Message.Type.UserControl, message.Tag);
        }

        public Unit TellSystem(SystemMessage message, ProcessId sender)
        {
            message.ConversationId = ConversationId;
            message.SessionId      = SessionId.Map(static x => x.Value).IfNoneUnsafe(message.SessionId);
            Cluster.PublishToChannel(
                ActorInboxCommon.ClusterSystemInboxNotifyKey(ProcessId),
                RemoteMessageDTO.Create(message, ProcessId, sender, Message.Type.System, message.Tag, SessionId, ConversationId, 0));
            return unit;
        }

        public Unit Tell(object message, Schedule schedule, ProcessId sender, string inbox, Message.Type type, Message.TagSpec tag) =>
            schedule == Schedule.Immediate
                ? TellNoIO(message, sender, inbox, type, tag)
                : schedule.Type == Schedule.PersistenceType.Ephemeral
                    ? LocalScheduler.Push(schedule, ProcessId, m => TellNoIO(m, sender, inbox, type, tag), message)
                    : DoSchedule(message, schedule, sender, type, tag);

        Unit TellNoIO(object message, ProcessId sender, string inbox, Message.Type type, Message.TagSpec tag)
        {
            ValidateMessageType(message, sender);
            var dto = RemoteMessageDTO.Create(message, ProcessId, sender, type, tag, SessionId, ConversationId, 0);
            var inboxKey = ActorInboxCommon.ClusterInboxKey(ProcessId, inbox);
            var inboxNotifyKey = ActorInboxCommon.ClusterInboxNotifyKey(ProcessId, inbox);
            Cluster.Enqueue(inboxKey, dto);
            var clientsReached = Cluster.PublishToChannel(inboxNotifyKey, dto.MessageId);
            return unit;
        }

        Unit DoSchedule(object message, Schedule schedule, ProcessId sender, Message.Type type, Message.TagSpec tag) =>
            DoScheduleNoIO(message, sender, schedule, type, tag);

        Unit DoScheduleNoIO(object message, ProcessId sender, Schedule schedule, Message.Type type, Message.TagSpec tag)
        {
            var inboxKey = ActorInboxCommon.ClusterScheduleKey(ProcessId);
            var id = schedule.Key ?? Guid.NewGuid().ToString();
            var current = Cluster.GetHashField<RemoteMessageDTO>(inboxKey, id);

            message = current.Match(
                Some: last =>
                {
                    var a = MessageSerialiser.DeserialiseMsg(last, ProcessId) as UserMessage;
                    var m = a == null
                        ? message
                        : schedule.Fold(a.Content, message);
                    return m;
                },
                None: () => schedule.Fold(schedule.Zero, message));

            ValidateMessageType(message, sender);

            var dto = RemoteMessageDTO.Create(message, ProcessId, sender, type, tag, SessionId, ConversationId, schedule.Due.Ticks);

            tell(ProcessId.Take(1).Child("system").Child("scheduler"), Scheduler.Msg.AddToSchedule(inboxKey, id, dto));

            //Cluster.HashFieldAddOrUpdate(inboxKey, id, dto);
            return unit;
        }

        public Unit Ask(object message, ProcessId sender) =>
            TellNoIO(message, sender, "user", Message.Type.User, Message.TagSpec.UserAsk);

        public Unit Publish(object message) =>
            ignore(Cluster.PublishToChannel(ActorInboxCommon.ClusterPubSubKey(ProcessId), message));

        public Unit Kill() =>
            TellSystem(SystemMessage.ShutdownProcess(false), Self);

        public Unit Shutdown() =>
            TellSystem(SystemMessage.ShutdownProcess(true), Self);

        public int GetInboxCount() =>
            Cluster.QueueLength(ActorInboxCommon.ClusterUserInboxKey(ProcessId));

        public Unit Watch(ProcessId pid) =>
            TellSystem(SystemMessage.Watch(pid), pid);

        public Unit UnWatch(ProcessId pid) =>
            TellSystem(SystemMessage.UnWatch(pid), pid);

        public Unit DispatchWatch(ProcessId watching) =>
            TellSystem(SystemMessage.DispatchWatch(watching), watching);

        public Unit DispatchUnWatch(ProcessId watching) =>
            TellSystem(SystemMessage.DispatchUnWatch(watching), watching);

        public bool IsLocal => 
            false;

        public bool Exists =>
            Cluster.Exists(ActorInboxCommon.ClusterMetaDataKey(ProcessId));

        public IEnumerable<Type> GetValidMessageTypes()
        {
            if (Cluster.Exists(ActorInboxCommon.ClusterMetaDataKey(ProcessId)))
            {
                var meta = Cluster.GetValue<ProcessMetaData>(ActorInboxCommon.ClusterMetaDataKey(ProcessId));
                if (meta == null)
                {
                    return new Type[0];
                }

                return meta.MsgTypeNames.Map(Type.GetType).ToArray();
            }
            else
            {
                return new Type[0];
            }
        }

        public bool Ping() =>
            Exists && Pinger.DoPing(ProcessId).Wait();
    }
}
