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
using System.Collections.Concurrent;
using LanguageExt;

namespace Echo
{
    class ActorInboxLocal<S, T> : ILocalActorInbox
    {
        const int MaxSysInboxSize = 100;
        
        readonly ConcurrentQueue<UserControlMessage> userInboxQueue = new();
        readonly ConcurrentQueue<SystemMessage> sysInboxQueue = new();

        Actor<S, T> actor;
        IActorSystem system;
        ActorItem parent;
        Option<ICluster> cluster;
        int maxMailboxSize;
        bool shutdownRequested = false;
        volatile int paused = 1;
        volatile int drainingUserQueue = 0;
        volatile int drainingSystemQueue = 0;

        /// <summary>
        /// Start up the inbox
        /// </summary>
        public Unit Startup(IActor process, IActorSystem system, ActorItem parent, Option<ICluster> cluster, int maxMailboxSize)
        {
            this.cluster = cluster;
            this.parent  = parent;
            this.actor   = (Actor<S, T>)process;
            this.system  = system; 
            this.maxMailboxSize = maxMailboxSize == -1
                                      ? ActorContext.System(actor.Id).Settings.GetProcessMailboxSize(actor.Id)
                                      : maxMailboxSize;

            Unpause();
            DrainUserQueue();
            DrainSystemQueue();
            return unit;
        }

        /// <summary>
        /// Shut down the inbox
        /// </summary>
        public Unit Shutdown() =>
            ignore(shutdownRequested = true);

        /// <summary>
        /// Get the sender or NoSender if invalid
        /// </summary>
        ProcessId GetSender(ProcessId sender) =>
            sender = sender.IsValid
                ? sender
                : Self.IsValid
                    ? Self
                    : ProcessId.NoSender;

        /// <summary>
        /// Posts a user ask into the user queue (non-blocking)
        /// </summary>
        public Unit Ask(object message, ProcessId sender, Option<SessionId> sessionId, long conversationId) =>
            ActorInboxCommon.PreProcessMessage<T>(sender, actor.Id, message, sessionId, conversationId).IfSome(TellUserControl);

        /// <summary>
        /// Posts a user tell into the user queue (non-blocking)
        /// </summary>
        public Unit Tell(object message, ProcessId sender, Option<SessionId> sessionId, long conversationId) =>
            ActorInboxCommon.PreProcessMessage<T>(sender, actor.Id, message, sessionId, conversationId).IfSome(TellUserControl);
        
        /// <summary>
        /// Posts a user-control message into the user queue (non-blocking)
        /// </summary>
        public Unit TellUserControl(UserControlMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (userInboxQueue.Count >= maxMailboxSize) throw new ProcessInboxFullException(actor.Id, MailboxSize, "user");
            if (shutdownRequested) throw new ProcessShutdownException(actor.Id);
            userInboxQueue.Enqueue(message);
            return DrainUserQueue();
        }

        /// <summary>
        /// Posts a message into the system queue (non-blocking)
        /// </summary>
        public Unit TellSystem(SystemMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (sysInboxQueue.Count >= MaxSysInboxSize) throw new ProcessInboxFullException(actor.Id, MaxSysInboxSize, "system");
            if (shutdownRequested) throw new ProcessShutdownException(actor.Id);
            sysInboxQueue.Enqueue(message);
            return DrainSystemQueue();
        }

        /// <summary>
        /// If the draining of the queue isn't running, fire it off asynchronously
        /// </summary>
        Unit DrainUserQueue()
        {
            if (userInboxQueue.Count > 0 &&
                !shutdownRequested && 
                !IsPaused &&
                system.IsActive && 
                Interlocked.CompareExchange(ref drainingUserQueue, 1, 0) == 0)
            {
                Task.Run(DrainUserQueueAsync);
            }
            return unit;
        }

        /// <summary>
        /// If the draining of the queue isn't running, fire it off asynchronously
        /// </summary>
        Unit DrainSystemQueue()
        {
            if (sysInboxQueue.Count > 0 &&
                system.IsActive && 
                Interlocked.CompareExchange(ref drainingSystemQueue, 1, 0) == 0)
            {
                Task.Run(DrainSystemQueueAsync);
            }
            return unit;
        }

        /// <summary>
        /// Walk the user message queue and process them one by one.  Return when we're done
        /// </summary>
        async ValueTask<Unit> DrainUserQueueAsync()
        {
            bool first = true;
            while (userInboxQueue.Count > 0 &&
                   !shutdownRequested &&
                   !IsPaused &&
                   system.IsActive &&
                   (first || Interlocked.CompareExchange(ref drainingUserQueue, 1, 0) == 0))
            {
                first = false;
                
                try
                {
                    while (true)
                    {
                        if (system.IsActive && !shutdownRequested && !IsPaused && userInboxQueue.TryPeek(out var msg))
                        {
                            try
                            {
                                switch (await ActorInboxCommon.UserMessageInbox(actor, this, msg, parent).ConfigureAwait(false))
                                {
                                    case InboxDirective.Default:
                                        userInboxQueue.TryDequeue(out _);
                                        break;

                                    case InboxDirective.Pause:
                                        userInboxQueue.TryDequeue(out _);
                                        Pause();
                                        return unit;

                                    case InboxDirective.PushToFrontOfQueue:
                                        break;

                                    case InboxDirective.Shutdown:
                                        userInboxQueue.TryDequeue(out _);
                                        Shutdown();
                                        return unit;

                                    default:
                                        throw new InvalidOperationException("unknown directive");
                                }
                            }
                            catch (Exception e)
                            {
                                userInboxQueue.TryDequeue(out _);
                                logSysErr(e);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    Interlocked.CompareExchange(ref drainingUserQueue, 0, 1);
                }

                // If we're processing a lot, let's give the scheduler a chance to do something else
                Thread.Yield();
            }
            return unit;
        }
        
        /// <summary>
        /// Walk the system message queue and process them one by one.  Return when we're done
        /// </summary>
        async ValueTask<Unit> DrainSystemQueueAsync()
        {
            bool first = true;
            while (sysInboxQueue.Count > 0 &&
                   system.IsActive &&
                   (first || Interlocked.CompareExchange(ref drainingSystemQueue, 1, 0) == 0))
            {
                first = false;
                try
                {
                    while (true)
                    {
                        if (system.IsActive && !shutdownRequested && sysInboxQueue.TryDequeue(out var msg))
                        {
                            try
                            {
                                switch (await ActorInboxCommon.SystemMessageInbox(actor, this, msg, parent).ConfigureAwait(false))
                                {
                                    case InboxDirective.Pause:
                                        Pause();
                                        return unit;
                                    case InboxDirective.Shutdown:
                                        Shutdown();
                                        return unit;
                                }
                            }
                            catch (Exception e)
                            {
                                logSysErr(e);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    Interlocked.CompareExchange(ref drainingSystemQueue, 0, 1);
                }
            }
            return unit;
        }        

        /// <summary>
        /// Mailbox size
        /// </summary>
        int MailboxSize => 
            maxMailboxSize < 0
                ? ActorContext.System(actor.Id).Settings.GetProcessMailboxSize(actor.Id)
                : maxMailboxSize;

        /// <summary>
        /// True if paused
        /// </summary>
        public bool IsPaused =>
            paused == 1;

        /// <summary>
        /// Pause the queue
        /// </summary>
        /// <returns></returns>
        public Unit Pause()
        {
            if (Interlocked.CompareExchange(ref paused, 1, 0) == 0)
            {
                actor.Pause();
            }
            return unit;
        }

        /// <summary>
        /// Unpause the queue
        /// </summary>
        /// <returns></returns>
        public Unit Unpause()
        {
            if (Interlocked.CompareExchange(ref paused, 0, 1) == 1)
            {
                actor.UnPause();
                DrainUserQueue();
            }
            return unit;
        }

        /// <summary>
        /// Number of unprocessed items
        /// </summary>
        public int Count =>
            userInboxQueue.Count;

        public IEnumerable<Type> GetValidMessageTypes() =>
            new[] { typeof(T) };

        string ClusterKey => actor.Id.Path + "-inbox";
        string ClusterNotifyKey => actor.Id.Path + "-inbox-notify";

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
