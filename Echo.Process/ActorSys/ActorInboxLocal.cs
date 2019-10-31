//using Microsoft.FSharp.Control;
using System;
using System.Linq;
using System.Threading;
using System.Reflection;
using static LanguageExt.Prelude;
using static Echo.Process;
using Newtonsoft.Json;
using Echo.ActorSys;
using System.Threading.Tasks;
using System.Collections.Generic;
using LanguageExt;

namespace Echo
{
    class ActorInboxLocal<S, T> : IActorInbox, ILocalActorInbox
    {
        PausableBlockingQueue<UserControlMessage> userInbox;
        PausableBlockingQueue<SystemMessage> sysInbox;

        Actor<S, T> actor;
        ActorItem parent;
        Option<ICluster> cluster;
        int maxMailboxSize;

        public Unit Startup(IActor process, ActorItem parent, Option<ICluster> cluster, int maxMailboxSize)
        {
            if (Active)
            {
                Shutdown();
            }
            this.cluster = cluster;
            this.parent = parent;
            this.actor = (Actor<S, T>)process;
            this.maxMailboxSize = maxMailboxSize == -1
                ? ActorContext.System(actor.Id).Settings.GetProcessMailboxSize(actor.Id)
                : maxMailboxSize;

            userInbox = new PausableBlockingQueue<UserControlMessage>(this.maxMailboxSize);
            sysInbox = new PausableBlockingQueue<SystemMessage>(this.maxMailboxSize);

            var obj = new ThreadObj { Actor = actor, Inbox = this, Parent = parent };
            userInbox.ReceiveAsync(obj, (state, msg) => ActorInboxCommon.UserMessageInbox(state.Actor, state.Inbox, msg, state.Parent));
            sysInbox.ReceiveAsync(obj, (state, msg) => ActorInboxCommon.SystemMessageInbox(state.Actor, state.Inbox, msg, state.Parent));

            return unit;
        }

        class ThreadObj
        {
            public Actor<S, T> Actor;
            public ActorInboxLocal<S, T> Inbox;
            public ActorItem Parent;
        }

        public Unit Shutdown()
        {
            userInbox?.Cancel();
            sysInbox?.Cancel();
            userInbox = null;
            sysInbox = null;

            return unit;
        }

        public IEnumerable<Type> GetValidMessageTypes() =>
            new[] { typeof(T) };

        string ClusterKey => actor.Id.Path + "-inbox";
        string ClusterNotifyKey => actor.Id.Path + "-inbox-notify";

        public bool Active => 
            userInbox != null;

        ProcessId GetSender(ProcessId sender) =>
            sender = sender.IsValid
                ? sender
                : Self.IsValid
                    ? Self
                    : ProcessId.NoSender;

        public Unit Ask(object message, ProcessId sender, Option<SessionId> sessionId)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (userInbox != null)
            {
                try
                {
                    return ActorInboxCommon.PreProcessMessage<T>(sender, actor.Id, message, sessionId)
                                           .IfSome(msg => userInbox?.Post(msg));
                }
                catch (QueueFullException)
                {
                    throw new ProcessInboxFullException(actor.Id, MailboxSize, "user");
                }
            }
            return unit;
        }

        public Unit Tell(object message, ProcessId sender, Option<SessionId> sessionId)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (userInbox != null)
            {
                try
                {
                    return ActorInboxCommon.PreProcessMessage<T>(sender, actor.Id, message, sessionId)
                                           .IfSome(msg => userInbox?.Post(msg));
                }
                catch(QueueFullException)
                {
                    throw new ProcessInboxFullException(actor.Id, MailboxSize, "user");
                }
            }
            return unit;
        }

        public Unit TellUserControl(UserControlMessage message, Option<SessionId> sessionId)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (userInbox != null)
            {
                try
                {
                    message.SessionId = message.SessionId ?? sessionId.Map(s => s.Value).IfNoneUnsafe(message.SessionId);
                    userInbox?.Post(message);
                }
                catch (QueueFullException)
                {
                    throw new ProcessInboxFullException(actor.Id, MailboxSize, "user");
                }
            }
            return unit;
        }

        public Unit TellSystem(SystemMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (sysInbox != null)
            {
                try
                {
                    sysInbox?.Post(message);
                }
                catch (QueueFullException)
                {
                    throw new ProcessInboxFullException(actor.Id, MailboxSize, "system");
                }
            }
            return unit;
        }

        int MailboxSize => 
            maxMailboxSize < 0
                ? ActorContext.System(actor.Id).Settings.GetProcessMailboxSize(actor.Id)
                : maxMailboxSize;

        public bool IsPaused
        {
            get
            {
                return userInbox?.IsPaused ?? false;
            }
            private set
            {
                if (value)
                {
                    userInbox?.Pause();
                }
                else
                {
                    userInbox?.UnPause();
                }
            }
        }

        public Unit Pause()
        {
            IsPaused = true;
            return unit;
        }

        public Unit Unpause()
        {
            IsPaused = false;

            // Wake up the user inbox to process any messages that have
            // been waiting patiently.
            TellUserControl(UserControlMessage.Null, None);

            return unit;
        }

        public void Dispose()
        {
            userInbox?.Cancel();
            sysInbox?.Cancel();
            userInbox = null;
            sysInbox = null;
        }

        /// <summary>
        /// Number of unprocessed items
        /// </summary>
        public int Count =>
            userInbox?.Count ?? 0;

        public Either<string, bool> HasStateTypeOf<TState>() =>
            TypeHelper.HasStateTypeOf(
                typeof(TState),
                typeof(S).Cons(
                    typeof(S).GetTypeInfo().ImplementedInterfaces
                    ).ToArray()
                );

        public Either<string, bool> CanAcceptMessageType<TMsg>() =>
            TypeHelper.IsMessageValidForProcess(typeof(TMsg), new[] { typeof(T) });

        public object ValidateMessageType(object message, ProcessId sender)
        {
            var res = TypeHelper.IsMessageValidForProcess(message, new[] { typeof(T) });
            res.IfLeft(err =>
            {
                throw new ProcessException($"{err } for Process ({actor.Id}).", actor.Id.Path, sender.Path, null);
            });
            return res.IfLeft(() => null);
        }
    }
}
