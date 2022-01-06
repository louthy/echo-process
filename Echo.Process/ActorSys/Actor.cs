using System;
using System.Threading;
using System.Reflection;
using static LanguageExt.Prelude;
using static Echo.Process;
using System.Reactive.Subjects;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Echo.Config;
using LanguageExt.ClassInstances;
using LanguageExt;
using OpenTracing;

namespace Echo
{
    /// <summary>
    /// Actor state machine 
    /// </summary>
    internal static class AState
    {
        public const int Shutdown = 0;
        public const int SettingUp = 1;
        public const int Running = 2;
        public const int ShuttingDown = 3;
    }

    /// <summary>
    /// Message state machine 
    /// </summary>
    internal static class MState
    {
        public const int Paused = 0;
        public const int WaitingForMessage = 1;
        public const int ProcessingMessage = 2;
    }

    /// <summary>
    /// Internal class that represents the state of a single process.
    /// </summary>
    /// <typeparam name="S">State</typeparam>
    /// <typeparam name="T">Message type</typeparam>
    class Actor<S, T> : IActor
    {
        readonly Func<S, T, ValueTask<S>> inboxFn;
        readonly Func<S, ProcessId, ValueTask<S>> termFn;
        readonly Func<IActor, ValueTask<S>> setupFn;
        readonly Func<S, ValueTask<Unit>> shutdownFn;
        readonly ProcessFlags flags;
        readonly Subject<object> publishSubject = new Subject<object>();
        readonly Subject<object> stateSubject = new Subject<object>();
        readonly Option<ICluster> cluster;
        readonly AtomHashMap<string, ActorItem> children = AtomHashMap<string, ActorItem>();
        readonly AtomHashMap<string, IDisposable> subs = AtomHashMap<string, IDisposable>();
        readonly IActorSystem sys;
        Option<S> state;
        StrategyState strategyState = StrategyState.Empty;
        bool remoteSubsAcquired;
        volatile int astatus;
        volatile int mstatus;
        readonly ISpanBuilder traceSetup;
        readonly ISpanBuilder traceInbox;

        internal Actor(
            Option<ICluster> cluster,
            ActorItem parent,
            ProcessName name,
            Func<S, T, ValueTask<S>> inbox,
            Func<IActor, ValueTask<S>> setup,
            Func<S, ValueTask<Unit>> shutdown,
            Func<S, ProcessId, ValueTask<S>> term,
            State<StrategyContext, Unit> strategy,
            ProcessFlags flags,
            ProcessSystemConfig settings,
            IActorSystem sys
            )
        {
            astatus      = AState.Shutdown;
            mstatus      = MState.Paused;
            setupFn      = setup ?? throw new ArgumentNullException(nameof(setup));
            inboxFn      = inbox ?? throw new ArgumentNullException(nameof(inbox));
            this.sys     = sys;
            Id           = parent.Actor.Id[name];
            traceSetup   = ProcessConfig.Tracer?.BuildSpan($"{Id}.setup");
            traceInbox   = ProcessConfig.Tracer?.BuildSpan($"{Id}.inbox");
            this.cluster = cluster;
            this.flags   = flags == ProcessFlags.Default
                               ? settings.GetProcessFlags(Id)
                               : flags;
            termFn       = term;
            Parent       = parent;
            Name         = name;
            Strategy     = strategy;
            
            shutdownFn = async (S s) =>
                         {
                             try
                             {
                                 if(shutdown != null) await shutdown(s).ConfigureAwait(false);
                             }
                             catch (Exception e)
                             {
                                 logErr(e);
                             }
                             return unit;
                         };
            
            SetupRemoteSubscriptions(cluster, flags);
        }

        /// <summary>
        /// Paused flag
        /// </summary>
        public bool IsPaused =>
            mstatus == MState.Paused;

        /// <summary>
        /// Start up - placeholder
        /// </summary>
        public async ValueTask<InboxDirective> Startup()
        {
            // Protect against multiple entry
            if (state.IsSome || Interlocked.CompareExchange(ref astatus, AState.SettingUp, AState.Shutdown) != AState.Shutdown)
            {
                return InboxDirective.Default;
            }

            var savedReq = ActorContext.Request.CurrentRequest;
            var savedFlags = ActorContext.Request.ProcessFlags;
            var savedMsg = ActorContext.Request.CurrentMsg;

            try
            {
                ActorContext.Request.CurrentRequest = null;
                ActorContext.Request.ProcessFlags = flags;
                ActorContext.Request.CurrentMsg = null;
                
                var stateValue = await GetState().ConfigureAwait(false);
                try
                {
                    if (notnull(stateValue))
                    {
                        stateSubject.OnNext(stateValue);
                    }
                }
                catch (Exception e)
                {
                    // Not our errors, so just log and move on
                    logErr(e);
                }

                // Success, so now we're ready to receive messages
                Interlocked.CompareExchange(ref mstatus, MState.WaitingForMessage, MState.Paused);
                Interlocked.CompareExchange(ref astatus, AState.Running, AState.SettingUp);
                
                return InboxDirective.Default;
            }
            catch (Exception e)
            {
                var directive = RunStrategy(
                    Id,
                    Parent.Actor.Id,
                    Sender,
                    Parent.Actor.Children.Values.Map(n => n.Actor.Id).Filter(x => x != Id),
                    e,
                    null,
                    Parent.Actor.Strategy
                );

                if(!(e is ProcessKillException)) tell(sys.Errors, e);

                return InboxDirective.Pause; // we give this feedback because Strategy will handle unpause
            }
            finally
            {
                // Make sure we're not hanging in the SettingUp phase, go back to uninitialised if we are
                // because there was an error somewhere.
                if (Interlocked.CompareExchange(ref astatus, AState.Shutdown, AState.SettingUp) == AState.SettingUp)
                {
                    Interlocked.CompareExchange(ref mstatus, MState.WaitingForMessage, MState.Paused);
                }

                ActorContext.Request.CurrentRequest = savedReq;
                ActorContext.Request.ProcessFlags = savedFlags;
                ActorContext.Request.CurrentMsg = savedMsg;
            }
        }

        /// <summary>
        /// Failure strategy
        /// </summary>
        State<StrategyContext, Unit> strategy;
        public State<StrategyContext, Unit> Strategy
        {
            get => strategy ?? sys.Settings.GetProcessStrategy(Id);
            private set => strategy = value;
        }

        public Unit AddSubscription(ProcessId pid, IDisposable sub) =>
            subs.AddOrUpdate(pid.Path,
                             exist => {
                                 exist?.Dispose();
                                 return sub;
                             },
                             sub);

        public Unit RemoveSubscription(ProcessId pid) =>
            subs.Swap(s => {
                          if (s.Find(pid.Path).Case is IDisposable d) d?.Dispose();
                          return s.Remove(pid.Path);
                      });

        Unit RemoveAllSubscriptions()
        {
            var snapshot = subs.ToHashMap();
            subs.Clear();
            snapshot.Iter(x => x?.Dispose());
            return unit;
        }

        public ProcessFlags Flags => 
            flags;

        string StateKey => 
            Id.Path + "@state";

        void SetupRemoteSubscriptions(Option<ICluster> cluster, ProcessFlags flags)
        {
            if (remoteSubsAcquired) return;

            cluster.IfSome(c =>
            {
                // Watches for local state-changes and persists them
                if ((flags & ProcessFlags.PersistState) == ProcessFlags.PersistState)
                {
                    try
                    {
                        stateSubject.Subscribe(state => c.SetValue(StateKey, state));
                    }
                    catch (Exception e)
                    {
                        logSysErr(e);
                    }
                }

                // Watches for local state-changes and publishes them remotely
                if ((flags & ProcessFlags.RemoteStatePublish) == ProcessFlags.RemoteStatePublish)
                {
                    try
                    {
                        stateSubject.Subscribe(state => c.PublishToChannel(ActorInboxCommon.ClusterStatePubSubKey(Id), state));
                    }
                    catch (Exception e)
                    {
                        logSysErr(e);
                    }
                }

                // Watches for publish events and remotely publishes them
                if ((flags & ProcessFlags.RemotePublish) == ProcessFlags.RemotePublish)
                {
                    try
                    {
                        publishSubject.Subscribe(msg => c.PublishToChannel(ActorInboxCommon.ClusterPubSubKey(Id), msg));
                    }
                    catch (Exception e)
                    {
                        logSysErr(e);
                    }
                }
            });

            remoteSubsAcquired = true;
        }

        async ValueTask<S> GetState()
        {
            var res = state.Case switch
                      {
                          S s => s,
                          _   => await InitState().ConfigureAwait(false)
                      };
            state = res;
            return res;
        }

        async ValueTask<S> InitState()
        {
            S state;

            var span = traceSetup?.StartActive();
            try
            {
                SetupRemoteSubscriptions(cluster, flags);

                if (cluster.IsSome && ((flags & ProcessFlags.PersistState) == ProcessFlags.PersistState))
                {
                    try
                    {
                        logInfo($"Restoring state: {StateKey}");

                        state = cluster.IfNoneUnsafe(() => null).Exists(StateKey)
                                    ? cluster.IfNoneUnsafe(() => null).GetValue<S>(StateKey)
                                    : await setupFn(this).ConfigureAwait(false);

                    }
                    catch (Exception e)
                    {
                        logSysErr(e);
                        state = await setupFn(this).ConfigureAwait(false);
                    }
                }
                else
                {
                    state = await setupFn(this).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                throw new ProcessSetupException(Id.Path, e);
            }
            finally
            {
                span?.Dispose();
            }

            try
            {
                stateSubject.OnNext(state);
            }
            catch (Exception ue)
            {
                // Not our errors, so just log and move on
                logErr(ue);
            }
            return state;
        }

        /// <summary>
        /// Publish observable stream
        /// </summary>
        public IObservable<object> PublishStream => publishSubject;

        /// <summary>
        /// State observable stream
        /// </summary>
        public IObservable<object> StateStream => stateSubject;

        /// <summary>
        /// Publish to the PublishStream
        /// </summary>
        public Unit Publish(object message)
        {
            try
            { 
                publishSubject.OnNext(message);
            }
            catch (Exception ue)
            {
                // Not our errors, so just log and move on
                logErr(ue);
            }
            return unit;
        }

        /// <summary>
        /// Process path
        /// </summary>
        public ProcessId Id { get; }

        /// <summary>
        /// Process name
        /// </summary>
        public ProcessName Name { get; }

        /// <summary>
        /// Parent process
        /// </summary>
        public ActorItem Parent { get; }

        /// <summary>
        /// Child processes
        /// </summary>
        public HashMap<string, ActorItem> Children =>
            children.ToHashMap();

        /// <summary>
        /// Waits for any message being processed to finish
        /// </summary>
        void SpinUntilPaused()
        {
            SpinWait sw = default;
            while (Interlocked.CompareExchange(ref mstatus, MState.Paused, MState.WaitingForMessage) != MState.WaitingForMessage)
            {
                // Something else might make us paused, so exit if that happens
                if (mstatus == MState.Paused) return;
                sw.SpinOnce();
            }
        }

        /// <summary>
        /// Clears the state (keeps the mailbox items)
        /// </summary>
        public async ValueTask<Unit> Restart(bool unpauseAfterRestart) =>
            await RestartInternal(unpauseAfterRestart, true).ConfigureAwait(false);
 
        /// <summary>
        /// Clears the state (keeps the mailbox items)
        /// </summary>
        async ValueTask<Unit> RestartInternal(bool unpauseAfterRestart, bool waitUntilPaused)
        {
            // Make sure we're not hanging in the SettingUp phase, go back to uninitialised if we are
            // because there was an error somewhere.
            if(Interlocked.CompareExchange(ref astatus, AState.ShuttingDown, AState.Running) == AState.Running)
            {
                if (waitUntilPaused)
                {
                    SpinUntilPaused();
                }

                try
                {
                    RemoveAllSubscriptions();
                    if (state.Case is S s) await shutdownFn(s).ConfigureAwait(false);
                    DisposeState();
                    foreach (var kid in Children)
                    {
                        kill(kid.Value.Actor.Id);
                    }
                }
                finally
                {
                    Interlocked.CompareExchange(ref astatus, AState.Shutdown, AState.ShuttingDown);
                }
                tellSystem(Id, SystemMessage.StartupProcess(unpauseAfterRestart)); 
            }
            return unit;
        }

        /// <summary>
        /// Disowns a child process
        /// </summary>
        public Unit UnlinkChild(ProcessId pid) =>
            children.Remove(pid.Name.Value);

        /// <summary>
        /// Gains a child process
        /// </summary>
        public Unit LinkChild(ActorItem item) =>
            children.AddOrUpdate(item.Actor.Id.Name.Value, item);

        /// <summary>
        /// Pause the process
        /// </summary>
        /// <returns>True if the process paused, False if it was already paused</returns>
        public bool Pause() =>
            Interlocked.Exchange(ref mstatus, MState.Paused) != MState.Paused;

        /// <summary>
        /// Unpause the process
        /// </summary>
        /// <returns>True if the process un-paused, False if it was already un-paused</returns>
        public bool UnPause() =>
            Interlocked.CompareExchange(ref mstatus, MState.WaitingForMessage, MState.Paused) == MState.Paused;

        /// <summary>
        /// Add a watcher of this Process
        /// </summary>
        /// <param name="pid">Id of the Process that will watch this Process</param>
        public Unit AddWatcher(ProcessId pid) =>
            sys.AddWatcher(pid, Id);

        /// <summary>
        /// Remove a watcher of this Process
        /// </summary>
        /// <param name="pid">Id of the Process that will stop watching this Process</param>
        public Unit RemoveWatcher(ProcessId pid) =>
            sys.RemoveWatcher(pid, Id);

        public Unit DispatchWatch(ProcessId pid)
        {
            sys.GetDispatcher(pid).Watch(Id);
            return ActorContext.System(Id).AddWatcher(pid, Id);
        }

        public Unit DispatchUnWatch(ProcessId pid)
        {
            sys.GetDispatcher(pid).UnWatch(Id);
            return ActorContext.System(Id).RemoveWatcher(pid, Id);
        }

        /// <summary>
        /// Shutdown everything from this node down
        /// </summary>
        public async ValueTask<Unit> Shutdown(bool maintainState) =>
            await ShutdownInternal(maintainState, true).ConfigureAwait(false);

        /// <summary>
        /// Shutdown everything from this node down
        /// </summary>
        async ValueTask<Unit> ShutdownInternal(bool maintainState, bool waitForPaused)
        {
            // If we're not running, we must already be shutting down, or shutdown, so return
            if (Interlocked.CompareExchange(ref astatus, AState.ShuttingDown, AState.Running) != AState.Running) return unit;
            
            try
            {
                // At this point we've forced the messages to stop processing, now we wait for any existing message
                // being processed to finish, then we'll pause
                if (waitForPaused)
                {
                    SpinUntilPaused();
                }

                // Tell our parent to unlink us
                Process.tellSystem(Parent.Actor.Id, new SystemUnLinkChildMessage(Self));

                // Shutdown children
                var kids = Children;
                children.Clear();
                foreach (var child in kids)
                {
                    await child.Value.Actor.Shutdown(maintainState);
                }

                if (maintainState == false && Flags != ProcessFlags.Default)
                {
                    cluster.IfSome(c => {
                                       // TODO: Make this transactional 
                                       // {
                                       c.DeleteMany(
                                           StateKey,
                                           ActorInboxCommon.ClusterUserInboxKey(Id),
                                           ActorInboxCommon.ClusterSystemInboxKey(Id),
                                           ActorInboxCommon.ClusterMetaDataKey(Id),
                                           ActorInboxCommon.ClusterSettingsKey(Id));

                                       sys.DeregisterById(Id);

                                       // }

                                       sys.Settings.ClearInMemorySettingsOverride(ActorInboxCommon.ClusterSettingsKey(Id));
                                   });
                }

                RemoveAllSubscriptions();
                publishSubject.OnCompleted();
                stateSubject.OnCompleted();
                remoteSubsAcquired = false;
                strategyState      = StrategyState.Empty;
                if (state.Case is S s) await shutdownFn(s).ConfigureAwait(false);
                DisposeState();

                sys.DispatchTerminate(Id);

                return unit;
            }
            finally
            {
                Interlocked.CompareExchange(ref astatus, AState.Shutdown, AState.ShuttingDown);
            }
        }

        public async ValueTask<Unit> ProcessResponse(ActorResponse response) =>
            ignore(await ProcessMessage(response).ConfigureAwait(false));

        public Option<T> PreProcessMessageContent(object message)
        {
            if (message == null)
            {
                tell(sys.DeadLetters, DeadLetter.create(Sender, Self, $"Message is null for tell (expected {typeof(T)})"));
                return None;
            }

            if (typeof(T) != typeof(string) && message is string strmsg)
            {
                try
                {
                    // This allows for messages to arrive from JS and be dealt with at the endpoint 
                    // (where the type is known) rather than the gateway (where it isn't)
                    return Some(Deserialise.Object<T>(strmsg));
                }
                catch
                {
                    tell(sys.DeadLetters, DeadLetter.create(Sender, Self, $"Invalid message type for tell (expected {typeof(T)})", strmsg));
                    return None;
                }
            }

            if (!(message is T))
            {
                tell(sys.DeadLetters, DeadLetter.create(Sender, Self, $"Invalid message type for tell (expected {typeof(T)})", message));
                return None;
            }

            return Some((T)message);
        }

        public async ValueTask<InboxDirective> ProcessAsk(ActorRequest request)
        {
            // Make sure we only process one message at a time
            SpinWait sw = default;
            while (Interlocked.CompareExchange(ref mstatus, MState.ProcessingMessage, MState.WaitingForMessage) != MState.WaitingForMessage)
            {
                if (mstatus == MState.Paused) return InboxDirective.PushToFrontOfQueue;
                if (astatus != AState.Running) return InboxDirective.PushToFrontOfQueue;
                sw.SpinOnce();
            }

            var savedMsg = ActorContext.Request.CurrentMsg;
            var savedFlags = ActorContext.Request.ProcessFlags;
            var savedReq = ActorContext.Request.CurrentRequest;

            var span = traceInbox?.WithTag("type", "ask")
                                  .WithTag("message-type", request?.Message?.GetType().FullName)
                                  .WithTag("conversation-id", request?.ConversationId ?? 0)
                                  .WithTag("request-id", request?.RequestId ?? 0)
                                  .WithTag("reply-to", request?.ReplyTo.ToString() ?? "" )
                                  .StartActive();
            try
            {
                ActorContext.Request.CurrentRequest = request;
                ActorContext.Request.ProcessFlags = flags;
                ActorContext.Request.CurrentMsg = request.Message;

                if (typeof(T) != typeof(string) && request.Message is string)
                {
                    state = await PreProcessMessageContent(request.Message).ToAsync().MatchAsync(
                        Some: async tmsg =>
                        {
                            var stateIn = await GetState().ConfigureAwait(false);
                            var stateOut = await inboxFn(stateIn, tmsg).ConfigureAwait(false);
                            try
                            {
                                if (!default(EqDefault<S>).Equals(stateOut, stateIn))
                                {
                                    stateSubject.OnNext(stateOut);
                                }
                            }
                            catch (Exception ue)
                            {
                                // Not our errors, so just log and move on
                                logErr(ue);
                            }

                            return Some(stateOut);
                        },
                        None: async () =>
                        {
                            replyError(new AskException($"Can't ask {Id.Path}, message is not {typeof(T).GetTypeInfo().Name} : {request.Message}"));
                            return await new ValueTask<Option<S>>(state);
                        }).ConfigureAwait(false);
                }
                else if (request.Message is T msg)
                {
                    var stateIn  = await GetState().ConfigureAwait(false);
                    var stateOut = await inboxFn(stateIn, msg).ConfigureAwait(false);
                    try
                    {
                        if (!default(EqDefault<S>).Equals(stateOut, stateIn))
                        {
                            stateSubject.OnNext(stateOut);
                        }
                    }
                    catch (Exception ue)
                    {
                        // Not our errors, so just log and move on
                        logErr(ue);
                    }

                    state = stateOut;
                }
                else if (request.Message is Message m)
                {
                    await ProcessSystemMessage(m).ConfigureAwait(false);
                }
                else
                {
                    // Failure to deserialise is not our problem, its the sender's
                    // so we don't throw here.
                    replyError(new AskException($"Can't ask {Id.Path}, message is not {typeof(T).GetTypeInfo().Name} : {request.Message}"));
                    return InboxDirective.Default;
                }

                strategyState = strategyState.With(
                    Failures: 0,
                    FirstFailure: DateTime.MaxValue,
                    LastFailure: DateTime.MaxValue,
                    BackoffAmount: 0 * seconds
                );
            }
            catch (Exception e)
            {
                replyError(e);
                return await DefaultErrorHandler(request, e).ConfigureAwait(false);
            }
            finally
            {
                span?.Dispose();
 
                ActorContext.Request.CurrentMsg = savedMsg;
                ActorContext.Request.ProcessFlags = savedFlags;
                ActorContext.Request.CurrentRequest = savedReq;
                
                // Go back to waiting for a message
                Interlocked.CompareExchange(ref mstatus, MState.WaitingForMessage, MState.ProcessingMessage);

            }

            return InboxDirective.Default;
        }

        async ValueTask<Unit> ProcessSystemMessage(Message message)
        {
            switch (message.Tag)
            {
                case Message.TagSpec.GetChildren:
                    return replyIfAsked(Children);
                
                case Message.TagSpec.ShutdownProcess:
                    return replyIfAsked(await Shutdown(false).ConfigureAwait(false));
                
                default:
                    return unit;
            }
        }

        public async ValueTask<InboxDirective> ProcessTerminated(ProcessId pid)
        {
            if (termFn == null) return InboxDirective.Default;

            // Make sure we only process one message at a time
            SpinWait sw = default;
            while (Interlocked.CompareExchange(ref mstatus, MState.ProcessingMessage, MState.WaitingForMessage) != MState.WaitingForMessage)
            {
                if (mstatus == MState.Paused) return InboxDirective.PushToFrontOfQueue;
                if (astatus != AState.Running) return InboxDirective.PushToFrontOfQueue;
                sw.SpinOnce();
            }
            
            var savedReq   = ActorContext.Request.CurrentRequest;
            var savedFlags = ActorContext.Request.ProcessFlags;
            var savedMsg   = ActorContext.Request.CurrentMsg;

            var span = traceInbox?.WithTag("type", "terminated")
                                  .WithTag("pid", pid.ToString())
                                  .StartActive();
            
            try
            {
                ActorContext.Request.CurrentRequest = null;
                ActorContext.Request.ProcessFlags   = flags;
                ActorContext.Request.CurrentMsg     = pid;

                var stateIn = await GetState().ConfigureAwait(false);
                var stateOut = await termFn(stateIn, pid).ConfigureAwait(false);
                state = stateOut;

                try
                {
                    if (!default(EqDefault<S>).Equals(stateOut, stateIn))
                    {
                        stateSubject.OnNext(stateOut);
                    }
                }
                catch (Exception ue)
                {
                    // Not our errors, so just log and move on
                    logErr(ue);
                }

                strategyState = strategyState.With(
                    Failures: 0,
                    FirstFailure: DateTime.MaxValue,
                    LastFailure: DateTime.MaxValue,
                    BackoffAmount: 0 * seconds
                    );
            }
            catch (Exception e)
            {
                return await DefaultErrorHandler(pid, e).ConfigureAwait(false);
            }
            finally
            {
                span?.Dispose();
                ActorContext.Request.CurrentRequest = savedReq;
                ActorContext.Request.ProcessFlags   = savedFlags;
                ActorContext.Request.CurrentMsg     = savedMsg;
                
                // Go back to waiting for a message
                Interlocked.CompareExchange(ref mstatus, MState.WaitingForMessage, MState.ProcessingMessage);
            }
            return astatus == AState.Shutdown 
                       ? InboxDirective.Shutdown
                       : InboxDirective.Default;
        }

        async ValueTask<InboxDirective> DefaultErrorHandler(object message, Exception e)
        {
            var directive = await RunStrategy(
                Id,
                Parent.Actor.Id,
                Sender,
                Parent.Actor.Children.Values.Map(n => n.Actor.Id).Filter(x => x != Id),
                e,
                message,
                Parent.Actor.Strategy).ConfigureAwait(false);
            if (!(e is ProcessKillException)) tell(sys.Errors, e);

            return astatus == AState.Shutdown 
                       ? InboxDirective.Shutdown
                       : directive;
        }

        /// <summary>
        /// Process an inbox message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async ValueTask<InboxDirective> ProcessMessage(object message)
        {
            // Make sure we only process one message at a time
            SpinWait sw = default;
            while (Interlocked.CompareExchange(ref mstatus, MState.ProcessingMessage, MState.WaitingForMessage) != MState.WaitingForMessage)
            {
                if (mstatus == MState.Paused) return InboxDirective.PushToFrontOfQueue;
                if (astatus != AState.Running) return InboxDirective.PushToFrontOfQueue;
                sw.SpinOnce();
            }

            var savedConversationId = ActorContext.ConversationId;
            var savedReq            = ActorContext.Request.CurrentRequest;
            var savedFlags          = ActorContext.Request.ProcessFlags;
            var savedMsg            = ActorContext.Request.CurrentMsg;

            var span = traceInbox?.WithTag("type", "tell")
                                  .WithTag("message-type", message?.GetType().FullName)
                                  .WithTag("conversation-id", savedConversationId)
                                  .WithTag("request-id", savedReq?.RequestId ?? 0)
                                  .WithTag("reply-to", savedReq?.ReplyTo.ToString() ?? "")
                                  .StartActive();

            try
            {
                ActorContext.Request.CurrentRequest = null;
                ActorContext.Request.ProcessFlags   = flags;
                ActorContext.Request.CurrentMsg     = message;

                if (message is T)
                {
                    var stateIn  = await GetState().ConfigureAwait(false);
                    var stateOut = await inboxFn(stateIn, (T) message).ConfigureAwait(false);
                    state = stateOut;
                    try
                    {
                        if (!default(EqDefault<S>).Equals(stateOut, stateIn))
                        {
                            stateSubject.OnNext(stateOut);
                        }
                    }
                    catch (Exception ue)
                    {
                        // Not our errors, so just log and move on
                        logErr(ue);
                    }
                }
                else if (typeof(T) != typeof(string) && message is string)
                {
                    state = await PreProcessMessageContent(message).ToAsync().MatchAsync(
                                Some: async tmsg => {
                                          var stateIn  = await GetState().ConfigureAwait(false);
                                          var stateOut = await inboxFn(stateIn, tmsg).ConfigureAwait(false);
                                          try
                                          {
                                              if (!default(EqDefault<S>).Equals(stateOut, stateIn))
                                              {
                                                  stateSubject.OnNext(stateOut);
                                              }
                                          }
                                          catch (Exception ue)
                                          {
                                              // Not our errors, so just log and move on
                                              logErr(ue);
                                          }

                                          return Some(stateOut);
                                      },
                                None: async () => await state.AsValueTask()).ConfigureAwait(false);
                }
                else if (message is Message m)
                {
                    await ProcessSystemMessage(m).ConfigureAwait(false);
                }
                else
                {
                    logErr($"Can't tell {Id.Path}, message is not {typeof(T).GetTypeInfo().Name} : {message}");
                    return InboxDirective.Default;
                }

                strategyState = strategyState.With(
                    Failures: 0,
                    FirstFailure: DateTime.MaxValue,
                    LastFailure: DateTime.MaxValue,
                    BackoffAmount: 0 * seconds
                );
            }
            catch (Exception e)
            {
                return await DefaultErrorHandler(message, e).ConfigureAwait(false);
            }
            finally
            {
                span?.Dispose();
                ActorContext.Request.CurrentRequest = savedReq;
                ActorContext.Request.ProcessFlags   = savedFlags;
                ActorContext.Request.CurrentMsg     = savedMsg;

                // Go back to waiting for a message
                Interlocked.CompareExchange(ref mstatus, MState.WaitingForMessage, MState.ProcessingMessage);
            }

            return astatus == AState.Shutdown 
                       ? InboxDirective.Shutdown
                       : InboxDirective.Default;
        }

        public async ValueTask<InboxDirective> RunStrategy(
            ProcessId pid,
            ProcessId parent,
            ProcessId sender,
            IEnumerable<ProcessId> siblings,
            Exception ex, 
            object message,
            State<StrategyContext, Unit> strategy
            )
        {
            try
            {
                // Build a strategy specifically for this event
                var failureStrategy = strategy.Failure(
                        pid,
                        parent,
                        sender,
                        siblings,
                        ex,
                        message
                    );

                // Invoke the strategy with the running state
                var result = failureStrategy.Run(strategyState);

                if (astatus == AState.ShuttingDown || astatus == AState.Shutdown)
                {
                    return InboxDirective.Default;
                }

                return await result.Value.Match(
                    Some: async decision =>
                    {
                        // Save the strategy state back to the actor
                        strategyState = result.State;

                        if (decision.ProcessDirective.Type != DirectiveType.Stop && decision.Pause > 0 * seconds)
                        {
                            decision.Affects.Iter(p => pause(p));

                            safedelay(() => {
                                          if (astatus == AState.ShuttingDown || astatus == AState.Shutdown) return;
                                          ignore(RunProcessDirective(pid, sender, ex, message, decision, true)); 
                                      },
                                      decision.Pause);

                            return InboxDirective.Pause | RunMessageDirective(pid, sender, decision, ex, message);
                        }
                        else
                        {
                            // Run the instruction for the Process (stop/restart/etc.)
                            try
                            {
                                await RunProcessDirective(pid, sender, ex, message, decision, false).ConfigureAwait(false);
                            }
                            catch (Exception e)
                            {
                                // Error in RunProcessDirective => log and still run RunMessageDirective
                                logErr("Strategy exception (RunProcessDirective) in " + Id, e);
                            }
                            // Run the instruction for the message (dead-letters/send-to-self/...)
                            return RunMessageDirective(pid, sender, decision, ex, message);
                        }
                    },
                    None: async () =>
                    {
                        logErr("Strategy failed (no decision) in " + Id);
                        return await InboxDirective.Default.AsValueTask();
                    },
                    Fail: async e =>
                    {
                        logErr("Strategy exception (decision error) " + Id, e);
                        return await InboxDirective.Default.AsValueTask();
                    });
            }
            catch (Exception e)
            {
                logErr("Strategy exception in " + Id, e);
                return InboxDirective.Default;
            }
        }

        InboxDirective RunMessageDirective(
            ProcessId pid,
            ProcessId sender,
            StrategyDecision decision, 
            Exception e, 
            object message
            )
        {
            var directive = decision.MessageDirective;
            switch (directive.Type)
            {
                case MessageDirectiveType.ForwardToParent:
                    tell(pid.Parent, message, sender);
                    return InboxDirective.Default;

                case MessageDirectiveType.ForwardToSelf:
                    tell(pid, message, sender);
                    return InboxDirective.Default;

                case MessageDirectiveType.ForwardToProcess:
                    tell(((ForwardToProcess)directive).ProcessId, message, sender);
                    return InboxDirective.Default;

                case MessageDirectiveType.StayInQueue:
                    return InboxDirective.PushToFrontOfQueue;

                default:
                    if (!(e is ProcessKillException))
                    {
                        tell(sys.DeadLetters, DeadLetter.create(sender, pid, e, "Process error: ", message));
                    }
                    return InboxDirective.Default;
            }
        }

        async ValueTask<Unit> RunProcessDirective(
            ProcessId pid,
            ProcessId sender,
            Exception e,
            object message,
            StrategyDecision decision,
            bool unPauseAfterRestart
        )
        {
            var directive = decision.ProcessDirective;

            // Find out the processes that this strategy affects and apply
            foreach (var cpid in decision.Affects.Filter(x => x != pid))
            {
                switch (directive.Type)
                {
                    case DirectiveType.Escalate:
                    case DirectiveType.Resume:
                        // Note: unpause probably won't do anything if unPauseAfterRestart==false because our strategy did not pause them (but unpause should not harm)
                        unpause(cpid);
                        break;
                    case DirectiveType.Restart:
                        restart(cpid);
                        break;
                    case DirectiveType.Stop:
                        kill(cpid);
                        break;
                }
            }

            switch (directive.Type)
            {
                case DirectiveType.Escalate:
                    return tellSystem(Parent.Actor.Id, SystemMessage.ChildFaulted(pid, sender, e, message), Self);
                case DirectiveType.Resume:
                    // Note: unpause should not be necessary if unPauseAfterRestart==false because our strategy did not pause before (but unpause should not harm)
                    return unpause(pid);
                case DirectiveType.Restart:
                    return await RestartInternal(unPauseAfterRestart, false).ConfigureAwait(false);
                case DirectiveType.Stop:
                    return await ShutdownInternal(false, false).ConfigureAwait(false);
                default:
                    return unit;
            }
        }

        public void Dispose()
        {
            RemoveAllSubscriptions();
            if(state.Case is S s) shutdownFn(s);
            DisposeState();
        }

        void DisposeState()
        {
            state.IfSome(s => (s as IDisposable)?.Dispose());
            state = None;
        }

        public ValueTask<InboxDirective> ChildFaulted(ProcessId pid, ProcessId sender, Exception ex, object message) =>
            RunStrategy(
                pid,
                Parent.Actor.Id,
                sender,
                Parent.Actor.Children.Values.Map(n => n.Actor.Id).Filter(x => x != Id),
                ex,
                message,
                Parent.Actor.Strategy);
    }
}
