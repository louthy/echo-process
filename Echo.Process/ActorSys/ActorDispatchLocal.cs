using LanguageExt;
using static LanguageExt.Prelude;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using static Echo.Process;

namespace Echo
{
    internal class ActorDispatchLocal : IActorDispatch
    {
        public readonly ILocalActorInbox Inbox;
        public readonly IActor Actor;
        public readonly Option<SessionId> SessionId;
        readonly bool transactionalIO;

        public ActorDispatchLocal(ActorItem actor, bool transactionalIO, Option<SessionId> sessionId)
        {
            this.transactionalIO = transactionalIO;
            SessionId = sessionId;
            Inbox = actor.Inbox as ILocalActorInbox;
            if (Inbox == null) throw new ArgumentException("Invalid (not local) ActorItem passed to LocalActorDispatch.");
            Actor = actor.Actor;
        }

        public IObservable<T> Observe<T>() =>
            from x in Actor.PublishStream
            where x is T
            select (T)x;

        public IObservable<T> ObserveState<T>() =>
            from x in Actor.StateStream
            where x is T
            select (T)x;

        public Either<string, bool> HasStateTypeOf<T>() =>
            Inbox.HasStateTypeOf<T>();

        public Either<string, bool> CanAccept<T>() =>
            Inbox.CanAcceptMessageType<T>();

        public Unit Tell(object message, Schedule schedule, ProcessId sender, Message.TagSpec tag)
        {
            var sessionId = ActorContext.SessionId;
            return LocalScheduler.Push(schedule, Actor.Id, m => Inbox.Tell(Inbox.ValidateMessageType(m, sender), sender, sessionId), message);
        }

        public Unit TellSystem(SystemMessage message, ProcessId sender) =>
            transactionalIO
                ? ProcessOp.IO(() => Inbox.TellSystem(message))
                : Inbox.TellSystem(message);

        public Unit TellUserControl(UserControlMessage message, ProcessId sender) =>
            transactionalIO
                ? ProcessOp.IO(() => Inbox.TellUserControl(message))
                : Inbox.TellUserControl(message);

        public Unit Ask(object message, ProcessId sender) =>
            Inbox.Ask(message, sender, ActorContext.SessionId);

        public Unit Kill() =>
            transactionalIO
                ? ProcessOp.IO(() => ShutdownProcess(false))
                : ShutdownProcess(false);

        public Unit Shutdown() =>
            transactionalIO
                ? ProcessOp.IO(() => ShutdownProcess(true))
                : ShutdownProcess(true);

        Unit ShutdownProcess(bool maintainState) =>
            ActorContext.System(Actor.Id).WithContext(
                new ActorItem(
                    Actor,
                    (IActorInbox)Inbox,
                    Actor.Flags
                    ),
                Actor.Parent,
                ProcessId.NoSender,
                null,
                SystemMessage.ShutdownProcess(maintainState),
                None,
                () => Actor.ShutdownProcess(maintainState)
            );

        public Map<string, ProcessId> GetChildren() =>
            Actor.Children.Map(a => a.Actor.Id);

        public Unit Publish(object message) =>
            transactionalIO
                ? ProcessOp.IO(() => Actor.Publish(message))
                : Actor.Publish(message);

        public int GetInboxCount() =>
            Inbox.Count;

        public Unit Watch(ProcessId pid) =>
            transactionalIO
                ? ProcessOp.IO(() => Actor.AddWatcher(pid))
                : Actor.AddWatcher(pid);

        public Unit UnWatch(ProcessId pid) =>
            transactionalIO
                ? ProcessOp.IO(() => Actor.RemoveWatcher(pid))
                : Actor.RemoveWatcher(pid);

        public Unit DispatchWatch(ProcessId watching) =>
            transactionalIO
                ? ProcessOp.IO(() => Actor.DispatchWatch(watching))
                : Actor.DispatchWatch(watching);

        public Unit DispatchUnWatch(ProcessId watching) =>
            transactionalIO
                ? ProcessOp.IO(() => Actor.DispatchUnWatch(watching))
                : Actor.DispatchUnWatch(watching);

        public bool IsLocal => 
            true;

        public bool Exists =>
            true;

        public IEnumerable<Type> GetValidMessageTypes() =>
            Inbox.GetValidMessageTypes();

        public bool Ping() =>
            Exists;
    }
}
