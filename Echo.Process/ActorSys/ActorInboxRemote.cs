using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static Echo.Process;
using static LanguageExt.Prelude;
using System.Collections.Concurrent;
using Echo.ActorSys;
using LanguageExt;

namespace Echo
{
    class ActorInboxRemote<S,T> : IActorInbox
    {
        ICluster cluster;
        Actor<S, T> actor;
        IActorSystem system;
        ActorItem parent;
        int maxMailboxSize;
        bool shutdownRequested = false;
        readonly ConcurrentQueue<RemoteMessageDTO> sysInboxQueue = new();
        volatile int drainingUserQueue = 0;
        volatile int drainingSystemQueue = 0;

        public Unit Startup(IActor process, IActorSystem system, ActorItem parent, Option<ICluster> cluster, int maxMailboxSize)
        {
            if (cluster.IsNone) throw new Exception("Remote inboxes not supported when there's no cluster");
            this.actor   = (Actor<S, T>)process;
            this.system  = system; 
            this.cluster = cluster.IfNoneUnsafe(() => null);

            this.maxMailboxSize = maxMailboxSize == -1
                ? ActorContext.System(actor.Id).Settings.GetProcessMailboxSize(actor.Id)
                : maxMailboxSize;

            this.parent = parent;

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
            cluster.SubscribeToChannel<string>(ActorInboxCommon.ClusterUserInboxNotifyKey(actor.Id)).Subscribe(_ => DrainUserQueue());
            DrainUserQueue();
        }

        /// <summary>
        /// Size of user mailbox
        /// </summary>
        int MailboxSize =>
            maxMailboxSize < 0
                ? ActorContext.System(actor.Id).Settings.GetProcessMailboxSize(actor.Id)
                : maxMailboxSize;

        /// <summary>
        /// True if paused
        /// </summary>
        public bool IsPaused =>
            actor?.IsPaused ?? true;

        /// <summary>
        /// Pause the inbox
        /// </summary>
        public Unit Pause()
        {
            if (actor?.Pause() ?? false)
            {
                cluster?.UnsubscribeChannel(ActorInboxCommon.ClusterUserInboxNotifyKey(actor.Id));
            }
            return default;
        }

        /// <summary>
        /// Unpause the inbox
        /// </summary>
        public Unit Unpause()
        {
            if (actor?.UnPause() ?? false)
            {
                SubscribeToUserInboxChannel();
                DrainUserQueue();
            }
            return default;
        }

        /// <summary>
        /// Enqueues the system message and wakes up the system-queue process-loop if necessary
        /// </summary>
        void PostSysInbox(RemoteMessageDTO msg)
        {
            sysInboxQueue.Enqueue(msg);
            DrainSystemQueue();
        }

        /// <summary>
        /// If the draining of the queue isn't running, fire it off asynchronously
        /// </summary>
        Unit DrainSystemQueue()
        {
            if (system.IsActive && 
                sysInboxQueue.Count > 0 &&
                Interlocked.CompareExchange(ref drainingSystemQueue, 1, 0) == 0)
            {
                Task.Run(DrainSystemQueueAsync);
            }
            return unit;
        }

        /// <summary>
        /// System inbox
        /// </summary>
        async ValueTask<Unit> DrainSystemQueueAsync()
        {
            bool first = true;
            while (system.IsActive &&
                   sysInboxQueue.Count > 0 &&
                   (first || Interlocked.CompareExchange(ref drainingSystemQueue, 1, 0) == 0))
            {
                first = false;
                try
                {
                    // Keep processing whilst we're not shutdown or there's something in the queue
                    while (!shutdownRequested && system.IsActive)
                    {
                        if (sysInboxQueue.TryDequeue(out var dto))
                        {
                            if (dto == null)
                            {
                                // Failed to deserialise properly
                                continue;
                            }

                            if (dto.Tag == 0 && dto.Type == 0)
                            {
                                // Message is bad
                                tell(ActorContext.System(actor.Id).DeadLetters, DeadLetter.create(dto.Sender, actor.Id, null, "Failed to deserialise message: ", dto));
                                return unit;
                            }

                            try
                            {
                                var msg = MessageSerialiser.DeserialiseMsg(dto, actor.Id);
                                if (msg is SystemMessage sysMsg)
                                {
                                    sysMsg.ConversationId = dto.ConversationId;

                                    // Run the system inbox
                                    switch (await ActorInboxCommon.SystemMessageInbox(actor, this, sysMsg, parent).ConfigureAwait(false))
                                    {
                                        case InboxDirective.Pause:
                                            Pause();
                                            return unit;
                                        case InboxDirective.Shutdown:
                                            Shutdown();
                                            return unit;
                                        default: continue;
                                    }
                                }
                                else
                                {
                                    // Failed to deal with the message, it's not a SystemMessage, so post to dead letters
                                    tell(ActorContext.System(actor.Id).DeadLetters, DeadLetter.create(dto.Sender, actor.Id, "Remote message inbox: not a system message.", msg));
                                }
                            }
                            catch (Exception e)
                            {
                                // Failed to deal with the message, so post to dead letters
                                tell(ActorContext.System(actor.Id).DeadLetters, DeadLetter.create(dto.Sender, actor.Id, e, "Remote message inbox.", dto));
                                logSysErr(e);
                            }
                        }
                        else
                        {
                            // Nothing left in the queue, so return
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    logSysErr(e);
                }
                finally
                {
                    Interlocked.CompareExchange(ref drainingSystemQueue, 0, 1);
                }
            }

            return unit;
        }

        Unit DrainUserQueue()
        {
            var key = ActorInboxCommon.ClusterUserInboxKey(actor.Id); 
            
            if (!shutdownRequested && 
                !IsPaused && 
                system.IsActive && 
                (cluster?.QueueLength(key) ?? 0) > 0 &&
                Interlocked.CompareExchange(ref drainingUserQueue, 1, 0) == 0)
            {
                Task.Run(() => DrainUserQueueAsync(key));
            }
            return unit;
        }

        async ValueTask<Unit> DrainUserQueueAsync(string key)
        {
            bool first = true;
            while (!shutdownRequested &&
                   !IsPaused &&
                   system.IsActive &&
                   (cluster?.QueueLength(key) ?? 0) > 0 &&
                   (first || Interlocked.CompareExchange(ref drainingUserQueue, 1, 0) == 0))
            {
                first = false;
                try
                {
                    var inbox = this;
                    var count = cluster?.QueueLength(key) ?? 0;

                    while (!IsPaused && !shutdownRequested && system.IsActive && count > 0)
                    {
                        if (ActorInboxCommon.GetNextMessage(cluster, actor.Id, key).Case is ValueTuple<RemoteMessageDTO, Message> m)
                        {
                            var dto = m.Item1;
                            var msg = m.Item2;
                            msg.ConversationId = dto.ConversationId;
                            try
                            {
                                var directive = msg.MessageType switch
                                                {
                                                    Message.Type.User =>
                                                        await ActorInboxCommon.UserMessageInbox(actor, inbox, (UserControlMessage) msg, parent)
                                                                              .ConfigureAwait(false),

                                                    Message.Type.UserControl =>
                                                        await ActorInboxCommon.UserMessageInbox(actor, inbox, (UserControlMessage) msg, parent)
                                                                              .ConfigureAwait(false),

                                                    _ => InboxDirective.Default
                                                };

                                switch (directive)
                                {
                                    case InboxDirective.Default:
                                        cluster?.Dequeue<RemoteMessageDTO>(key);
                                        count--;
                                        break;
                                    case InboxDirective.Pause:
                                        cluster?.Dequeue<RemoteMessageDTO>(key);
                                        return Pause();

                                    case InboxDirective.PushToFrontOfQueue:
                                        break;

                                    case InboxDirective.Shutdown:
                                        cluster?.Dequeue<RemoteMessageDTO>(key);
                                        return Shutdown();

                                    default:
                                        throw new InvalidOperationException("unknown directive");
                                }
                            }
                            catch (Exception e)
                            {
                                cluster?.Dequeue<RemoteMessageDTO>(key);
                                count--;
                                var session = msg.SessionId == null ? None : Some(new SessionId(msg.SessionId));
                                await ActorContext.System(actor.Id)
                                                  .WithContext(new ActorItem(actor, inbox, actor.Flags),
                                                               parent,
                                                               dto.Sender,
                                                               msg as ActorRequest,
                                                               msg,
                                                               session,
                                                               dto.ConversationId,
                                                               () => replyErrorIfAsked(e).AsValueTask()).ConfigureAwait(false);

                                tell(ActorContext.System(actor.Id).DeadLetters, DeadLetter.create(dto.Sender, actor.Id, e, "Remote message inbox.", msg));
                                logSysErr(e);
                            }
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

        public Unit Shutdown()
        {
            shutdownRequested = true;
            Dispose();
            return unit;
        }

        public void Dispose()
        {
            cluster?.UnsubscribeChannel(ActorInboxCommon.ClusterUserInboxNotifyKey(actor.Id));
            cluster?.UnsubscribeChannel(ActorInboxCommon.ClusterSystemInboxNotifyKey(actor.Id));
            cluster = null;
        }
    }
}
