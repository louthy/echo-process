using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
//using Microsoft.FSharp.Control;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using static Echo.Process;
using static LanguageExt.Prelude;
using Newtonsoft.Json;
using Echo.ActorSys;
using LanguageExt;

namespace Echo
{
    /// <summary>
    /// This is both a local and remote inbox in one. 
    /// 
    /// TODO: Lots of cut n paste from the Local and Remote variants, need to factor out the 
    ///       common elements.
    /// </summary>
    class ActorInboxDual<S, T> : IActorInbox, ILocalActorInbox
    {
        const int MaxSysInboxSize = 100;
        readonly ConcurrentQueue<UserControlMessage> userInboxQueue = new();
        readonly ConcurrentQueue<SystemMessage> sysInboxQueue = new();
        ICluster cluster;
        Actor<S, T> actor;
        IActorSystem system;
        ActorItem parent;
        int maxMailboxSize;
        int paused = 1;
        bool shutdownRequested = false;
        volatile int drainingUserQueue = 0;
        volatile int drainingSystemQueue = 0;
        volatile int checkingRemoteInbox = 0;

        public Unit Startup(IActor process, IActorSystem system, ActorItem parent, Option<ICluster> cluster, int maxMailboxSize)
        {
            if (cluster.IsNone) throw new Exception("Remote inboxes not supported when there's no cluster");

            this.actor   = (Actor<S, T>)process;
            this.system  = system; 
            this.parent  = parent;
            this.cluster = cluster.IfNoneUnsafe(() => null);
            this.maxMailboxSize = maxMailboxSize == -1 
                ? ActorContext.System(actor.Id).Settings.GetProcessMailboxSize(actor.Id) 
                : maxMailboxSize;

            this.cluster.SetValue(ActorInboxCommon.ClusterMetaDataKey(actor.Id),
                                  new ProcessMetaData(
                                      new[] {typeof(T).AssemblyQualifiedName},
                                      typeof(S).AssemblyQualifiedName,
                                      typeof(S).GetTypeInfo().ImplementedInterfaces.Map(x => x.AssemblyQualifiedName).ToArray()));

            SubscribeToSysInboxChannel();
            SubscribeToUserInboxChannel();

            Unpause();

            DrainSystemQueue();
            DrainUserQueue();
 
            return unit;
        }

        void SubscribeToSysInboxChannel()
        {
            // System inbox is just listening to the notifications, that means that system
            // messages don't persist.
            cluster.UnsubscribeChannel(ActorInboxCommon.ClusterSystemInboxNotifyKey(actor.Id));
            cluster.SubscribeToChannel<RemoteMessageDTO>(ActorInboxCommon.ClusterSystemInboxNotifyKey(actor.Id)).Subscribe(PostSysInbox);
            DrainSystemQueue();
        }

        void SubscribeToUserInboxChannel()
        {
            cluster.UnsubscribeChannel(ActorInboxCommon.ClusterUserInboxNotifyKey(actor.Id));
            cluster.SubscribeToChannel<string>(ActorInboxCommon.ClusterUserInboxNotifyKey(actor.Id))
                   .Subscribe(msg => DrainRemoteInbox(ActorInboxCommon.ClusterUserInboxKey(actor.Id)));
            cluster.PublishToChannel(ActorInboxCommon.ClusterUserInboxNotifyKey(actor.Id), Guid.NewGuid().ToString());
            DrainUserQueue();
            DrainRemoteInbox(ActorInboxCommon.ClusterUserInboxKey(actor.Id));
        }

        /// <summary>
        /// User mailbox size
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
        /// Pauses the process so it doesn't process any messages (will still queue them)
        /// </summary>
        /// <returns></returns>
        public Unit Pause()
        {
            if (Interlocked.CompareExchange(ref paused, 1, 0) == 0)
            {
                cluster.UnsubscribeChannel(ActorInboxCommon.ClusterUserInboxNotifyKey(actor.Id));
            }
            return unit;
        }

        /// <summary>
        /// Unpause the process, and work through the backlog of messages
        /// </summary>
        public Unit Unpause()
        {
            if (Interlocked.CompareExchange(ref paused, 0, 1) == 1)
            {
                SubscribeToUserInboxChannel();
                DrainUserQueue();
            }
            return unit;
        }

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
        /// Enqueues the system message and wakes up the system-queue process-loop if necessary
        /// </summary>
        void PostSysInbox(RemoteMessageDTO dto)
        {
            try
            {
                if (dto == null)
                {
                    // Failed to deserialise properly
                    return;
                }

                if (dto.Tag == 0 && dto.Type == 0)
                {
                    // Message is bad
                    tell(ActorContext.System(actor.Id).DeadLetters, DeadLetter.create(dto.Sender, actor.Id, null, "Failed to deserialise message: ", dto));
                    return;
                }

                var msg = MessageSerialiser.DeserialiseMsg(dto, actor.Id);
                if (msg is SystemMessage sysMsg)
                {
                    sysInboxQueue.Enqueue(sysMsg);
                }
                else
                {
                    tell(ActorContext.System(actor.Id).DeadLetters, DeadLetter.create(dto.Sender, actor.Id, null, "Failed to deserialise message: ", dto));
                }

                DrainSystemQueue();
            }
            catch (Exception e)
            {
                logSysErr(e);
            }
        }        

        public Unit Shutdown()
        {
            Dispose();
            return unit;
        }

        public void Dispose()
        {
            cluster?.UnsubscribeChannel(ActorInboxCommon.ClusterUserInboxNotifyKey(actor.Id));
            cluster?.UnsubscribeChannel(ActorInboxCommon.ClusterSystemInboxNotifyKey(actor.Id));
            cluster = null;
        }

        /// <summary>
        /// Drains all messages from the persistent store into in-memory queues
        /// </summary>
        void DrainRemoteInbox(string key)
        {
            if (system.IsActive && 
                cluster.QueueLength(key) > 0 &&
                Interlocked.CompareExchange(ref checkingRemoteInbox, 1, 0) == 0)
            {
                DoDrainRemoteInbox(key);
            }
        }

        /// <summary>
        /// Drains all messages from the persistent store into in-memory queues
        /// </summary>
        public void DoDrainRemoteInbox(string key)
        {
            bool first = true;
            while (system.IsActive &&
                   cluster.QueueLength(key) > 0 &&
                   (first || Interlocked.CompareExchange(ref checkingRemoteInbox, 1, 0) == 0))
            {
                first = false;
                try
                {
                    int count = cluster.QueueLength(key);

                    while (!IsPaused && !shutdownRequested && system.IsActive && count > 0)
                    {
                        var pair = ActorInboxCommon.GetNextMessage(cluster, actor.Id, key);
                        pair.IfSome(x => cluster.Dequeue<RemoteMessageDTO>(key));

                        if (pair.Case is ValueTuple<RemoteMessageDTO, Message> m)
                        {
                            var msg = m.Item2;
                            switch (msg.MessageType)
                            {
                                case Message.Type.System:
                                    sysInboxQueue.Enqueue((SystemMessage) msg);
                                    DrainSystemQueue();
                                    break;
                                case Message.Type.User:
                                    userInboxQueue.Enqueue((UserControlMessage) msg);
                                    DrainUserQueue();
                                    break;
                                case Message.Type.UserControl:
                                    userInboxQueue.Enqueue((UserControlMessage) msg);
                                    DrainUserQueue();
                                    break;
                            }
                        }

                        count--;
                        if (count == 0)
                        {
                            count = cluster.QueueLength(key);
                        }
                    }
                }
                catch (Exception e)
                {
                    logSysErr($"CheckRemoteInbox failed for {actor.Id}", e);
                }
                finally
                {
                    Interlocked.CompareExchange(ref checkingRemoteInbox, 0, 1);
                }

                // If we're processing a lot, let's give the scheduler a chance to do something else
                Thread.Yield();
            }
        }

        /// <summary>
        /// If the draining of the queue isn't running, fire it off asynchronously
        /// </summary>
        Unit DrainUserQueue()
        {
            if (!shutdownRequested && 
                !IsPaused && 
                system.IsActive &&
                userInboxQueue.Count > 0 &&
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
            while (!shutdownRequested &&
                   !IsPaused &&
                   system.IsActive &&
                   userInboxQueue.Count > 0 &&
                   (first || Interlocked.CompareExchange(ref drainingUserQueue, 1, 0) == 0))
            {
                first = false;
                try
                {
                    while (true)
                    {
                        if (!shutdownRequested &&
                            !IsPaused &&
                            system.IsActive &&
                            userInboxQueue.TryPeek(out var msg))
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
                                        break;

                                    default:
                                        throw new InvalidOperationException("unknown directive");
                                }
                            }
                            catch (Exception e)
                            {
                                logSysErr($"{actor.Id}", e);
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
                        if (!shutdownRequested &&
                            system.IsActive &&
                            sysInboxQueue.TryDequeue(out var msg))
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
                                        break;
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
        /// Number of unprocessed items
        /// </summary>
        public int Count =>
            userInboxQueue.Count;

        public IEnumerable<Type> GetValidMessageTypes() =>
            new[] { typeof(T) };

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
                throw new ProcessException($"{err} for Process ({actor.Id}).", actor.Id.Path, sender.Path, null);
            });
            return res.IfLeft(() => null);
        }
    }
}
