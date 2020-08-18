using System;
using System.Threading;
using System.Reflection;
using static LanguageExt.Prelude;
using static Echo.ProcessAff;
using System.Reactive.Subjects;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Reactive.Linq;
using Echo.Config;
using LanguageExt.ClassInstances;
using LanguageExt;

namespace Echo
{
    class ActorState<S>
    {
        public static readonly ActorState<S> Default = new ActorState<S>(default, default, default, Echo.StrategyState.Empty, default);  
        
        public ActorState(HashMap<string, IDisposable> subs, HashMap<string, ActorItem> children, Option<S> state, StrategyState strategyState, Seq<IDisposable> remoteSubs)
        {
            Subs = subs;
            Children = children;
            State = state;
            StrategyState = strategyState;
            RemoteSubs = remoteSubs;
        }

        public readonly HashMap<string, IDisposable> Subs;
        public readonly HashMap<string, ActorItem> Children;
        public readonly Option<S> State;
        public readonly StrategyState StrategyState;
        public readonly Seq<IDisposable> RemoteSubs;

        public ActorState<S> With(
            HashMap<string, IDisposable>? Subs = null, 
            HashMap<string, ActorItem>? Children = null, 
            Option<S>? State = null, 
            StrategyState StrategyState = null, 
            Seq<IDisposable>? RemoteSubs = null) =>
            new ActorState<S>(
                Subs ?? this.Subs, 
                Children ?? this.Children, 
                State ?? this.State, 
                StrategyState ?? this.StrategyState, 
                RemoteSubs ?? this.RemoteSubs);
    }

    /// <summary>
    /// Internal class that represents the state of a single process.
    /// </summary>
    /// <typeparam name="S">State</typeparam>
    /// <typeparam name="T">Message type</typeparam>
    class Actor<RT, S, T> : IActor 
        where RT : struct, HasEcho<RT>
    {
        public readonly ProcessId Id;
        public readonly ProcessName Name;
        public readonly ActorItem Parent;
        public readonly Func<S, T, Aff<RT, S>> ActorFn;
        public readonly Func<S, ProcessId, Aff<RT, S>> TermFn;
        public readonly Func<IActor, Aff<RT, S>> SetupFn;
        public readonly Func<S, Aff<RT, Unit>> ShutdownFn;
        public readonly ProcessFlags Flags;
        public readonly State<StrategyContext, Unit> Strategy;
        public readonly Subject<object> PublishSubject;
        public readonly Subject<S> StateSubject;
        public readonly Atom<ActorState<S>> State;

        public Actor(
            ProcessId id,
            ProcessName name,
            ActorItem parent,
            Func<S, T, Aff<RT, S>> actorFn, 
            Func<S, ProcessId, Aff<RT, S>> termFn, 
            Func<IActor, Aff<RT, S>> setupFn, 
            Func<S, Aff<RT, Unit>> shutdownFn, 
            ProcessFlags flags, 
            State<StrategyContext, Unit> strategy,
            Subject<object> publishSubject, 
            Subject<S> stateSubject,
            Atom<ActorState<S>> state)
        {
            Id = id;
            Name = name;
            Parent = parent;
            ActorFn = actorFn;
            TermFn = termFn;
            SetupFn = setupFn;
            ShutdownFn = shutdownFn;
            Flags = flags;
            Strategy = strategy;
            PublishSubject = publishSubject;
            StateSubject = stateSubject;
            State = state;
        }
        
        public Aff<RT, Unit> InitState() =>
            State.SwapAff(state =>
                        from nwState1 in SuccessEff(disposeRemoteSubs(state))
                        from nwState2 in (Flags & ProcessFlags.PersistState) == ProcessFlags.PersistState
                                             ? from exists in Cluster.exists<RT>(stateKey(Id))
                                               from result in exists
                                                   ? Cluster.getValue<RT, S>(stateKey(Id))
                                                   : SetupFn(this)
                                               select result
                                             : SetupFn(this)
                        from remSubs  in setupRemoteSubs(Id, Flags, StateSubject)
                        select nwState1.With(State: nwState2, RemoteSubs: remSubs))
                 .Map(_ => unit);

        static ActorState<S> disposeRemoteSubs(ActorState<S> state)
        {
            state.RemoteSubs.Iter(s => s.Dispose());
            return state.With(RemoteSubs: Empty);
        }

        internal static string stateKey(ProcessId id) => 
            id.Path + "@state";
        
        internal static Aff<RT, Seq<IDisposable>> setupRemoteSubs(ProcessId id, ProcessFlags flags, Subject<S> stateSubject) =>

            // Watches for local state-changes and persists them
            from spers in (flags & ProcessFlags.PersistState) == ProcessFlags.PersistState
                ? logErr(
                    Eff<RT, IDisposable>(e => 
                        stateSubject.SelectMany(async state => await Cluster.setValue<RT, S>(stateKey(id), state).RunIO(e))
                            .Subscribe(state => { })))
                : SuccessEff<IDisposable>(null)
            
            // Watches for local state-changes and publishes them remotely
            from sdisp in (flags & ProcessFlags.RemoteStatePublish) == ProcessFlags.RemoteStatePublish
                ? logErr(
                    Eff<RT, IDisposable>(e => 
                        stateSubject.SelectMany(async state => await Cluster.publishToChannel<RT, S>(ActorInboxCommon.ClusterStatePubSubKey(id), state).RunIO(e))
                            .Subscribe(state => { })))
                : SuccessEff<IDisposable>(null)
                
            // Watches for publish events and remotely publishes them
            from pubd  in (flags & ProcessFlags.RemotePublish) == ProcessFlags.RemotePublish
                ? logErr(
                    Eff<RT, IDisposable>(e => 
                        stateSubject.SelectMany(async state => await Cluster.publishToChannel<RT, S>(ActorInboxCommon.ClusterPubSubKey(id), state).RunIO(e))
                            .Subscribe(state => { })))
                : SuccessEff<IDisposable>(null)
            
            select new [] {spers, sdisp, pubd}.Filter(notnull).ToSeq();        
        //     
        //     
        // {
        //     S state;
        //
        //     try
        //     {
        //         SetupRemoteSubscriptions(cluster, flags);
        //
        //         if (cluster.IsSome && ((flags & ProcessFlags.PersistState) == ProcessFlags.PersistState))
        //         {
        //             try
        //             {
        //                 logInfo($"Restoring state: {StateKey}");
        //
        //                 state = cluster.IfNoneUnsafe(() => null).Exists(StateKey)
        //                     ? cluster.IfNoneUnsafe(() => null).GetValue<S>(StateKey)
        //                     : setupFn(this);
        //
        //             }
        //             catch (Exception e)
        //             {
        //                 logSysErr(e);
        //                 state = setupFn(this);
        //             }
        //         }
        //         else
        //         {
        //             state = setupFn(this);
        //         }
        //
        //         ActorContext.Request.RunOps();
        //     }
        //     catch (Exception e)
        //     {
        //         throw new ProcessSetupException(Id.Path, e);
        //     }
        //
        //     try
        //     {
        //         stateSubject.OnNext(state);
        //     }
        //     catch (Exception ue)
        //     {
        //         // Not our errors, so just log and move on
        //         logErr(ue);
        //     }
        //     return state;
        // }          
    }

    static class ActorAff<RT, S, A> where RT : struct, HasEcho<RT>
    {
        public static Aff<RT, IActor> create(
            bool cluster,
            ActorItem parent,
            ProcessName name,
            Func<S, A, Aff<RT, S>> actor,
            Func<IActor, Aff<RT, S>> setup,
            Func<S, Aff<RT, Unit>> shutdown,
            Func<S, ProcessId, Aff<RT, S>> term,
            State<StrategyContext, Unit> strategy,
            ProcessFlags flags) =>
                from setupFn    in SuccessEff(setup ?? throw new ArgumentNullException(nameof(setup)))
                from actorFn    in SuccessEff(actor ?? throw new ArgumentNullException(nameof(actor)))
                from shutdownFn in SuccessEff(shutdown ?? throw new ArgumentNullException(nameof(shutdown))).Map(loggingShutdown)
                from sys        in ActorContextAff<RT>.LocalSystem
                from settings   in ActorContextAff<RT>.LocalSettings
                from id         in SuccessEff(parent.Actor.Id[name])
                from pflags     in flags == ProcessFlags.Default
                                       ? ProcessSystemConfigAff<RT>.getProcessFlags(id)
                                       : SuccessEff(flags)
                from stateSubj  in SuccessEff(new Subject<S>())
                from pubSubj    in SuccessEff(new Subject<object>())
                from remoteSubs in cluster
                                      ? Actor<RT, S, A>.setupRemoteSubs(id, flags, stateSubj)
                                      : SuccessEff(Seq<IDisposable>())
                from state      in SuccessEff(Atom(ActorState<S>.Default))
                select new Actor<RT, S, A>(id, name, parent, actorFn, term, setupFn, shutdownFn, flags, strategy, pubSubj, stateSubj, state) as IActor;
        
        
        static Func<S, Aff<RT, Unit>> loggingShutdown(Func<S, Aff<RT, Unit>> shutdown) =>
            state =>
                ProcessAff.logErr<RT, Unit>(shutdown(state));

 

        // static Aff<RT, S> getState =>
        //     from ctx in ActorContextAff<RT>.Request
        //     from res in ctx.Self.Actor.GetState<RT
        // {
        //     lock (sync)
        //     {
        //         var res = state.IfNoneUnsafe(InitState);
        //         state = res;
        //         return res;
        //     }
        // }

      
        
        /// <summary>
        /// Start up
        /// </summary>
        public static Aff<RT, InboxDirective> startup =>
            from dir in                             
        
        /// <summary>
        /// Start up - placeholder
        /// </summary>
        public InboxDirective Startup()
        {
            lock(sync)
            {
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return InboxDirective.Shutdown;
                }

                if (state.IsSome) return InboxDirective.Default;

                var savedReq = ActorContext.Request.CurrentRequest;
                var savedFlags = ActorContext.Request.ProcessFlags;
                var savedMsg = ActorContext.Request.CurrentMsg;

                try
                {
                    ActorContext.Request.CurrentRequest = null;
                    ActorContext.Request.ProcessFlags = flags;
                    ActorContext.Request.CurrentMsg = null;

                    var stateValue = GetState();
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
                    ActorContext.Request.CurrentRequest = savedReq;
                    ActorContext.Request.ProcessFlags = savedFlags;
                    ActorContext.Request.CurrentMsg = savedMsg;
                }
            }
        }

        /// <summary>
        /// Failure strategy
        /// </summary>
        State<StrategyContext, Unit> strategy;
        public State<StrategyContext, Unit> Strategy
        {
            get
            {
                return strategy ?? sys.Settings.GetProcessStrategy(Id);
            }
            private set
            {
                strategy = value;
            }
        }

        public Unit AddSubscription(ProcessId pid, IDisposable sub)
        {
            RemoveSubscription(pid);
            subs = subs.Add(pid.Path, sub);
            return unit;
        }

        public Unit RemoveSubscription(ProcessId pid)
        {
            subs.Find(pid.Path).IfSome(x => x.Dispose());
            subs = subs.Remove(pid.Path);
            return unit;
        }

        Unit RemoveAllSubscriptions()
        {
            subs.Iter(x => x.Dispose());
            subs = HashMap<string, IDisposable>();
            return unit;
        }

        public ProcessFlags Flags => 
            flags;


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
            children;

        public CancellationTokenSource CancellationTokenSource => cancellationTokenSource;

        /// <summary>
        /// Clears the state (keeps the mailbox items)
        /// </summary>
        public Unit Restart(bool unpauseAfterRestart)
        {
            lock (sync)
            {
                RemoveAllSubscriptions();
                state.IfSome(shutdownFn);
                DisposeState();
                foreach (var kid in Children)
                {
                    kill(kid.Value.Actor.Id);
                }
            }
            tellSystem(Id, SystemMessage.StartupProcess(unpauseAfterRestart)); 
            return unit;
        }

        /// <summary>
        /// Disowns a child process
        /// </summary>
        public Unit UnlinkChild(ProcessId pid)
        {
            children.Swap(c => c.Remove(pid.Name.Value));
            return unit;
        }

        /// <summary>
        /// Gains a child process
        /// </summary>
        public Unit LinkChild(ActorItem item)
        {
            children.Swap(c => c.AddOrUpdate(item.Actor.Id.Name.Value, item));
            return unit;
        }

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
        public Unit Shutdown(bool maintainState)
        {
            cancellationTokenSource.Cancel(); // this will signal other operations not to start processing more messages (ProcessMessage) or startup again (Startup), even if they already received something from the queue
            lock(sync)
            {
                if (maintainState == false && Flags != ProcessFlags.Default)
                {
                    cluster.IfSome(c =>
                    {
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
                strategyState = StrategyState.Empty;
                state.IfSome(shutdownFn);
                DisposeState();

                sys.DispatchTerminate(Id);

                return unit;
            }
        }

        public Option<T> PreProcessMessageContent(object message)
        {
            if (message == null)
            {
                tell(sys.DeadLetters, DeadLetter.create(Sender, Self, $"Message is null for tell (expected {typeof(T)})", message));
                return None;
            }

            if (typeof(T) != typeof(string) && message is string)
            {
                try
                {
                    // This allows for messages to arrive from JS and be dealt with at the endpoint 
                    // (where the type is known) rather than the gateway (where it isn't)
                    return Some(Deserialise.Object<T>((string)message));
                }
                catch
                {
                    tell(sys.DeadLetters, DeadLetter.create(Sender, Self, $"Invalid message type for tell (expected {typeof(T)})", message));
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

        public R ProcessRequest<R>(ProcessId pid, object message)
        {
            try
            {
                if (request != null)
                {
                    throw new Exception("async ask not allowed");
                }

                response = null;
                request = new AutoResetEvent(false);
                sys.Ask(pid, new ActorRequest(message, pid, Self, 0), Self);
                request.WaitOne(sys.Settings.Timeout);

                if (response == null)
                {
                    throw new TimeoutException("Request timed out");
                }
                else
                {
                    if (response.IsFaulted)
                    {
                        var ex = (Exception)response.Message;
                        throw new ProcessException($"Process issue: {ex.Message}", pid.Path, Self.Path, ex);
                    }
                    else
                    {
                        return (R)response.Message;
                    }
                }
            }
            finally
            {
                if (request != null)
                {
                    request.Dispose();
                    request = null;
                }
            }
        }

        public Unit ProcessResponse(ActorResponse response)
        {
            if (request == null)
            {
                ProcessMessage(response);
            }
            else
            {
                this.response = response;
                request.Set();
            }
            return unit;
        }

        public InboxDirective ProcessAsk(ActorRequest request)
        {
            lock (sync)
            {
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    replyError(new AskException(
                        $"Can't ask {Id.Path} because actor shutdown in progress"));
                    return InboxDirective.Shutdown;
                }

                var savedMsg = ActorContext.Request.CurrentMsg;
                var savedFlags = ActorContext.Request.ProcessFlags;
                var savedReq = ActorContext.Request.CurrentRequest;

                try
                {
                    ActorContext.Request.CurrentRequest = request;
                    ActorContext.Request.ProcessFlags = flags;
                    ActorContext.Request.CurrentMsg = request.Message;

                    //ActorContext.AssertSession();

                    if (typeof(T) != typeof(string) && request.Message is string)
                    {
                        state = PreProcessMessageContent(request.Message).Match(
                            Some: tmsg =>
                            {
                                var stateIn = GetState();
                                var stateOut = actorFn(stateIn, tmsg);
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

                                return stateOut;
                            },
                            None: () =>
                            {
                                replyError(new AskException(
                                    $"Can't ask {Id.Path}, message is not {typeof(T).GetTypeInfo().Name} : {request.Message}"));
                                return state;
                            }
                        );
                    }
                    else if (request.Message is T msg)
                    {
                        var stateIn = GetState();
                        var stateOut = actorFn(stateIn, msg);
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
                        ProcessSystemMessage(m);
                    }
                    else
                    {
                        // Failure to deserialise is not our problem, its the sender's
                        // so we don't throw here.
                        replyError(new AskException(
                            $"Can't ask {Id.Path}, message is not {typeof(T).GetTypeInfo().Name} : {request.Message}"));
                        return InboxDirective.Default;
                    }

                    strategyState = strategyState.With(
                        Failures: 0,
                        FirstFailure: DateTime.MaxValue,
                        LastFailure: DateTime.MaxValue,
                        BackoffAmount: 0 * seconds
                    );

                    ActorContext.Request.RunOps();
                }
                catch (Exception e)
                {
                    ActorContext.Request.SetOps(ProcessOpTransaction.Start(Id));
                    replyError(e);
                    ActorContext.Request.RunOps();
                    return DefaultErrorHandler(request, e);
                }
                finally
                {
                    ActorContext.Request.CurrentMsg = savedMsg;
                    ActorContext.Request.ProcessFlags = savedFlags;
                    ActorContext.Request.CurrentRequest = savedReq;
                }

                return InboxDirective.Default;
            }
        }

        void ProcessSystemMessage(Message message)
        {
            lock (sync)
            {
                switch (message.Tag)
                {
                    case Message.TagSpec.GetChildren:
                        replyIfAsked(Children);
                        break;
                    case Message.TagSpec.ShutdownProcess:
                        replyIfAsked(ShutdownProcess(false));
                        break;
                }
            }
        }

        public InboxDirective ProcessTerminated(ProcessId pid)
        {
            if (termFn == null) return InboxDirective.Default;

            lock (sync)
            {
                if (cancellationTokenSource.IsCancellationRequested) return InboxDirective.Shutdown;

                var savedReq   = ActorContext.Request.CurrentRequest;
                var savedFlags = ActorContext.Request.ProcessFlags;
                var savedMsg   = ActorContext.Request.CurrentMsg;

                try
                {
                    ActorContext.Request.CurrentRequest = null;
                    ActorContext.Request.ProcessFlags   = flags;
                    ActorContext.Request.CurrentMsg     = pid;

                    //ActorContext.AssertSession();

                    var stateIn = GetState();
                    var stateOut = termFn(stateIn, pid);
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

                    ActorContext.Request.RunOps();
                }
                catch (Exception e)
                {
                    return DefaultErrorHandler(pid, e);
                }
                finally
                {
                    ActorContext.Request.CurrentRequest = savedReq;
                    ActorContext.Request.ProcessFlags   = savedFlags;
                    ActorContext.Request.CurrentMsg     = savedMsg;
                }
                return InboxDirective.Default;
            }
        }

        InboxDirective DefaultErrorHandler(object message, Exception e)
        {
            // Wipe all transactional outputs because of the error
            ActorContext.Request.SetOps(ProcessOpTransaction.Start(Id));

            var directive = RunStrategy(
                Id,
                Parent.Actor.Id,
                Sender,
                Parent.Actor.Children.Values.Map(n => n.Actor.Id).Filter(x => x != Id),
                e,
                message,
                Parent.Actor.Strategy
            );
            if (!(e is ProcessKillException)) tell(sys.Errors, e);

            // Run any transactional outputs caused by the strategy computation
            ActorContext.Request.RunOps();
            return directive;
        }

        /// <summary>
        /// Process an inbox message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public InboxDirective ProcessMessage(object message)
        {
            lock (sync)
            {
                if (cancellationTokenSource.IsCancellationRequested) return InboxDirective.Shutdown;

                var savedReq = ActorContext.Request.CurrentRequest;
                var savedFlags = ActorContext.Request.ProcessFlags;
                var savedMsg = ActorContext.Request.CurrentMsg;

                try
                {
                    ActorContext.Request.CurrentRequest = null;
                    ActorContext.Request.ProcessFlags = flags;
                    ActorContext.Request.CurrentMsg = message;

                    if (message is T)
                    {
                        var stateIn = GetState();
                        var stateOut = actorFn(stateIn, (T)message);
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
                        state = PreProcessMessageContent(message).Match(
                                    Some: tmsg =>
                                    {
                                        var stateIn = GetState();
                                        var stateOut = actorFn(stateIn, tmsg);
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
                                        return stateOut;
                                    },
                                    None: () => state
                                );
                    }
                    else if (message is Message)
                    {
                        ProcessSystemMessage((Message)message);
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

                    ActorContext.Request.RunOps();
                }
                catch (Exception e)
                {
                    return DefaultErrorHandler(message, e);
                }
                finally
                {
                    ActorContext.Request.CurrentRequest = savedReq;
                    ActorContext.Request.ProcessFlags = savedFlags;
                    ActorContext.Request.CurrentMsg = savedMsg;
                }
                return InboxDirective.Default;
            }
        }

        public InboxDirective RunStrategy(
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

                return result.Value.Match(
                    Some: decision =>
                    {
                        // Save the strategy state back to the actor
                        strategyState = result.State;

                        if (decision.ProcessDirective.Type != DirectiveType.Stop && decision.Pause > 0 * seconds)
                        {
                            decision.Affects.Iter(p => pause(p));

                            var safeDelayDisposable = safedelay(
                                () => RunProcessDirective(pid, sender, ex, message, decision, true),
                                decision.Pause
                            );
                            cancellationTokenSource.Token.Register(() => safeDelayDisposable.Dispose());

                            return InboxDirective.Pause | RunMessageDirective(pid, sender, decision, ex, message);
                        }
                        else
                        {
                            // Run the instruction for the Process (stop/restart/etc.)
                            try
                            {
                                RunProcessDirective(pid, sender, ex, message, decision, false);
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
                    None: () =>
                    {
                        logErr("Strategy failed (no decision) in " + Id);
                        return InboxDirective.Default;
                    },
                    Fail: e =>
                    {
                        logErr("Strategy exception (decision error) " + Id, e);
                        return InboxDirective.Default;
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
                    tell((directive as ForwardToProcess).ProcessId, message, sender);
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

        void RunProcessDirective(
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
                    tellSystem(Parent.Actor.Id, SystemMessage.ChildFaulted(pid, sender, e, message), Self);
                    break;
                case DirectiveType.Resume:
                    // Note: unpause should not be necessary if unPauseAfterRestart==false because our strategy did not pause before (but unpause should not harm)
                    unpause(pid);
                    break;
                case DirectiveType.Restart:
                    Restart(unPauseAfterRestart);
                    break;
                case DirectiveType.Stop:
                    ShutdownProcess(false);
                    break;
            }
        }

        public Unit ShutdownProcess(bool maintainState)
        {
            cancellationTokenSource.Cancel();
            lock (sync)
            {
                return Parent.Actor.Children.Find(Name.Value).IfSome(self =>
                {
                    ShutdownProcessRec(self, sys.GetInboxShutdownItem().Map(x => (ILocalActorInbox)x.Inbox), maintainState);
                    Parent.Actor.UnlinkChild(Id);
                    children.Swap(_ => HashMap<string, ActorItem>());
                });
            }
        }

        void ShutdownProcessRec(ActorItem item, Option<ILocalActorInbox> inboxShutdown, bool maintainState)
        {
            var process = item.Actor;
            var inbox = item.Inbox;

            foreach (var child in process.Children.Values)
            {
                ShutdownProcessRec(child, inboxShutdown, maintainState);
            }

            inboxShutdown.Match(
                Some: ibs => ibs.Tell(inbox, ProcessId.NoSender, None),
                None: ()  => inbox.Dispose()
            );

            process.Shutdown(maintainState);
        }

        public void Dispose()
        {
            RemoveAllSubscriptions();
            state.IfSome(shutdownFn);
            DisposeState();
            cancellationTokenSource.Dispose();
        }

        void DisposeState()
        {
            state.IfSome(s => (s as IDisposable)?.Dispose());
            state = None;
        }

        public InboxDirective ChildFaulted(ProcessId pid, ProcessId sender, Exception ex, object message)
        {
            return RunStrategy(
                pid,
                Parent.Actor.Id,
                sender,
                Parent.Actor.Children.Values.Map(n => n.Actor.Id).Filter(x => x != Id),
                ex,
                message,
                Parent.Actor.Strategy
            );
        }
    }
}
