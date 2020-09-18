using System;
using System.Threading;
using System.Reflection;
using static LanguageExt.Prelude;
using System.Reactive.Subjects;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Reactive.Linq;
using Echo;
using Echo.Config;
using LanguageExt.ClassInstances;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;

namespace Echo
{
    /// <summary>
    /// Represents the entire state of a single actor process
    /// </summary>
    /// <typeparam name="RT">Runtime</typeparam>
    /// <typeparam name="S">State</typeparam>
    /// <typeparam name="A">Message type</typeparam>
    class Actor<RT, S, A> : IActor<RT> 
        where RT : struct, HasEcho<RT>
    {
        public readonly Func<S, A, Aff<RT, S>> ActorFn;
        public readonly Func<S, ProcessId, Aff<RT, S>> TermFn;
        public readonly Func<IActor<RT>, Aff<RT, S>> SetupFn;
        public readonly Func<S, Aff<RT, Unit>> ShutdownFn;
        public readonly Subject<object> PublishSubject;
        public readonly Subject<S> StateSubject;
        public readonly Atom<ActorState<S>> State;

        public ProcessId Id { get; }
        public ProcessName Name { get; }
        public ActorItem Parent { get; }
        public ProcessFlags Flags { get; }
        public State<StrategyContext, Unit> Strategy { get; }
        public HashMap<string, ActorItem> Children => State.Value.Children;
 
        public Actor(
            ProcessId id,
            ProcessName name,
            ActorItem parent,
            Func<S, A, Aff<RT, S>> actorFn, 
            Func<S, ProcessId, Aff<RT, S>> termFn, 
            Func<IActor<RT>, Aff<RT, S>> setupFn, 
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

        [Pure]
        public Aff<RT, S> AssertState =>
            State.Value.State.IsNone 
                ? InitState
                : SuccessEff(State.Value.State.ValueUnsafe());
        
        [Pure]
        public Aff<RT, S> InitState =>
            from astate in ProcessAff<RT>.catchAndLogErr(InitStateInternal, SuccessEff(State.Value))
            from _      in astate.State.Map(NextState).IfNone(unitEff)
            from state  in astate.State.Match(SuccessEff, FailEff<S>(Error.New("Setup failed, no state value available")))
            select state;

        [Pure]
        Aff<RT, ActorState<S>> InitStateInternal =>
            State.SwapAff(state =>
                from nwState1 in state.DisposeRemoteSubs()
                from nwState2 in Flags.HasPersistentState()
                                    ? from exists in Cluster.exists<RT>(stateKey(Id))
                                      from result in exists
                                                        ? Cluster.getValue<RT, S>(stateKey(Id))
                                                        : SetupFn(this)
                                      select result
                                    : SetupFn(this)
                from remSubs in setupRemoteSubs(Id, Flags, StateSubject)
                select nwState1.With(State: nwState2, RemoteSubs: remSubs));
        
        [Pure]
        public Eff<Unit> NextState(S state) =>
            ProcessEff.catchAndLogErr(Eff(() => { StateSubject.OnNext(state); return unit; }), unitEff);


        /// <summary>
        /// Start up - placeholder
        /// </summary>
        [Pure]
        public Aff<RT, InboxDirective> Startup =>
            AssertState.Match(Succ: _ => SuccessEff(InboxDirective.Default),
                              Fail: ActorAff<RT>.runStrategy)
                       .Flatten();

        /// <summary>
        /// Restarts the process (and shutdowns down all children)
        /// Clears the state, but keeps the mailbox items
        /// </summary>
        /// <param name="unpauseAfterRestart">if set to true then inbox shall be unpaused after starting up again</param>
        [Pure]
        public Aff<RT, Unit> Restart(bool unpauseAfterRestart) =>
            State.SwapAff(state =>
                    from ns1 in state.RemoveAllSubscriptions()
                    from __2 in ns1.State.Map(ShutdownFn).IfNone(unitEff)
                    from ns2 in ns1.Dispose()
                    from __3 in Children.Values.Map(c => ProcessAff<RT>.kill(c.Actor.Id)).SequenceParallel()
                    from __4 in ProcessAff<RT>.tellSystem(Id, SystemMessage.StartupProcess(unpauseAfterRestart))
                    select ns2)
                .Bind(_ => unitEff);

        [Pure]
        Aff<RT, Unit> ClusterShutdown() =>
            from sys in ActorContextAff<RT>.LocalSystem
            from __1 in Cluster.deleteMany<RT>(stateKey(Id),
                                                  ActorInboxCommon.ClusterUserInboxKey(Id),
                                                  ActorInboxCommon.ClusterSystemInboxKey(Id),
                                                  ActorInboxCommon.ClusterMetaDataKey(Id),
                                                  ActorInboxCommon.ClusterSettingsKey(Id))
            from __2 in ActorSystemAff<RT>.deregisterById(Id)
            from __3 in ProcessSystemConfigAff<RT>.clearInMemorySettingsOverride(ActorInboxCommon.ClusterSettingsKey(Id))
            select unit;
        
        /// <summary>
        /// Shutdown everything from this process and all its child processes
        /// </summary>
        [Pure]
        public Aff<RT, Unit> Shutdown(bool maintainState) =>
            State.SwapAff(state =>
                from __0 in Flags.HasPersistence() && !maintainState 
                               ? ClusterShutdown()
                               : unitEff
                from ns1 in state.RemoveAllSubscriptions()
                from __2 in Eff(() => { PublishSubject.OnCompleted(); return unit; })
                from __3 in Eff(() => { StateSubject.OnCompleted(); return unit; })
                from ns2 in SuccessEff(ns1.ResetStrategyState())
                from __5 in State.Value.State.Map(ShutdownFn).IfNone(unitEff)
                from __6 in ns2.DisposeState()
                from __7 in ActorSystemAff<RT>.dispatchTerminate(Id)
                select ns2)
                .Bind(_ => unitEff);
        
        /// <summary>
        /// Publish observable stream
        /// </summary>
        [Pure]
        public IObservable<object> PublishStream => 
            PublishSubject;

        /// <summary>
        /// State observable stream
        /// </summary>
        [Pure]
        public IObservable<object> StateStream => 
            StateSubject.Select(s => (object)s);

        /// <summary>
        /// Current state for the strategy system
        /// </summary>
        [Pure]
        public StrategyState StrategyState =>
            State.Value.StrategyState;

        /// <summary>
        /// Publish to the PublishStream
        /// </summary>
        [Pure]
        public Eff<Unit> Publish(object message) =>
            ProcessEff.catchAndLogErr(Eff(fun(() => PublishSubject.OnNext(message))), unitEff);
        
        /// <summary>
        /// Add a subscription
        /// </summary>
        /// <remarks>If one already exists then it is safely disposed</remarks>
        [Pure]
        public Eff<Unit> AddSubscription(ProcessId pid, IDisposable sub) =>
            State.SwapEff(state => state.AddSubscription(pid, sub)).Bind(_ => unitEff); 

        /// <summary>
        /// Remove a subscription and safely dispose it 
        /// </summary>
        [Pure]
        public Eff<Unit> RemoveSubscription(ProcessId pid) =>
            State.SwapEff(state => state.RemoveSubscription(pid)).Bind(_ => unitEff); 

        /// <summary>
        /// Safely dispose and remove all subscriptions
        /// </summary>
        /// <returns></returns>
        [Pure]
        public Eff<Unit> RemoveAllSubscriptions() =>
            State.SwapEff(state => state.RemoveAllSubscriptions()).Bind(_ => unitEff);

        /// <summary>
        /// Disowns a child process
        /// </summary>
        [Pure]
        public Eff<Unit> UnlinkChild(ProcessId pid) =>
            State.SwapEff(state => SuccessEff(state.UnlinkChild(pid))).Bind(_ => unitEff);

        /// <summary>
        /// Gains a child process
        /// </summary>
        [Pure]
        public Eff<Unit> LinkChild(ActorItem item) =>
            State.SwapEff(state => state.LinkChild(item)
                                        .Match(
                                            Some: SuccessEff, 
                                            None: () => FailEff<ActorState<S>>(Error.New($"Child process already exists with the same name: {item.Actor.Name}"))))
                 .Bind(_ => unitEff);

        /// <summary>
        /// Add a watcher of this Process
        /// </summary>
        /// <param name="pid">Id of the Process that will watch this Process</param>
        [Pure]
        public Eff<RT, Unit> AddWatcher(ProcessId pid) =>
            ActorSystemAff<RT>.addWatcher(pid, Id);

        /// <summary>
        /// Remove a watcher of this Process
        /// </summary>
        /// <param name="pid">Id of the Process that will stop watching this Process</param>
        [Pure]
        public Eff<RT, Unit> RemoveWatcher(ProcessId pid) =>
            ActorSystemAff<RT>.removeWatcher(pid, Id);

        /// <summary>
        /// Tell another process that we're watching it.  That means we get our Termination inbox called when the
        /// watched process dies.
        /// </summary>
        /// <param name="pid">Process to watch</param>
        [Pure]
        public Aff<RT, Unit> DispatchWatch(ProcessId pid) =>
            from d in ActorSystemAff<RT>.getDispatcher(pid)
            from a in d.Watch<RT>(Id)
            from b in ActorSystemAff<RT>.addWatcher(pid, Id)
            select unit; 

        [Pure]
        public Aff<RT, Unit> DispatchUnWatch(ProcessId pid) =>
            from d in ActorSystemAff<RT>.getDispatcher(pid)
            from a in d.UnWatch<RT>(Id)
            from b in ActorSystemAff<RT>.removeWatcher(pid, Id)
            select unit; 

        [Pure]
        static string stateKey(ProcessId id) => 
            id.Path + "@state";
        
        [Pure]
        internal static Aff<RT, Seq<IDisposable>> setupRemoteSubs(ProcessId id, ProcessFlags flags, Subject<S> stateSubject) =>

            // Watches for local state-changes and persists them
            from spers in flags.HasPersistentState()
                ? ProcessAff<RT>.logErr(
                    Eff<RT, IDisposable>(e => 
                        stateSubject.SelectMany(async state => await Cluster.setValue<RT, S>(stateKey(id), state).RunIO(e))
                            .Subscribe(state => { })))
                : SuccessEff<IDisposable>(null)
            
            // Watches for local state-changes and publishes them remotely
            from sdisp in flags.HasPublishRemoteState()
                ? ProcessAff<RT>.logErr(
                    Eff<RT, IDisposable>(e => 
                        stateSubject.SelectMany(async state => await Cluster.publishToChannel<RT, S>(ActorInboxCommon.ClusterStatePubSubKey(id), state).RunIO(e))
                            .Subscribe(state => { })))
                : SuccessEff<IDisposable>(null)
                
            // Watches for publish events and remotely publishes them
            from pubd  in flags.HasPublishRemote()
                ? ProcessAff<RT>.logErr(
                    Eff<RT, IDisposable>(e => 
                        stateSubject.SelectMany(async state => await Cluster.publishToChannel<RT, S>(ActorInboxCommon.ClusterPubSubKey(id), state).RunIO(e))
                            .Subscribe(state => { })))
                : SuccessEff<IDisposable>(null)
            
            select new [] {spers, sdisp, pubd}.Filter(notnull).ToSeq();

        [Pure]
        public Aff<RT, Unit> Dispose() =>
            from _1 in RemoveAllSubscriptions()
            from _2 in State.Value.State.Map(ShutdownFn).IfNone(SuccessAff(unit))
            from _3 in DisposeState()
            select unit;

        [Pure]
        Eff<Unit> DisposeState() =>
            State.SwapEff(s => s.Dispose())
                 .Map(_ => unit);
        
        [Pure]
        static Aff<RT, A> preProcessMessageContent(object message, string type) =>
            message switch {
                // Already what we need
                A amsg =>  SuccessEff(amsg),
                
                // Null, invalid
                null => ActorAff<RT>.writeDeadLetter<A>($"Message is null for {type} (expected {typeof(A)})", null),
                
                // String, try to convert it to an A
                string smsg when typeof(A) != typeof(string) =>
                    from sdmsg  in default(RT).SerialiseEff.Map(e => e.DeserialiseStructural<A>(smsg))
                    from rsdmsg in sdmsg.Match(SuccessEff, ActorAff<RT>.writeDeadLetter<A>($"Message failed to deserialise for {type} (expected {typeof(A)})", message))
                    select rsdmsg,

                // Unknown
                _ => ActorAff<RT>.writeDeadLetter<A>($"Message failed to deserialise for {type} (expected {typeof(A)})", message)
            };

        /// <summary>
        /// Announce the outState if it's different from the inState
        /// </summary>
        [Pure]
        Eff<RT, Unit> PublishNewState(S inState, S outState) =>
            default(EqDefault<S>).Equals(outState, inState)
                ? unitEff
                : NextState(outState);
        
        /// <summary>
        /// Clear the strategy state, so it has 0 retries, back-off, etc.
        /// </summary>
        [Pure]
        public Eff<Unit> ResetStrategyState() =>
            State.SwapEff(s => SuccessEff(s.ResetStrategyState()))
                 .Bind(_ => unitEff);

        /// <summary>
        /// Update the strategy state
        /// </summary>
        [Pure]
        public Eff<Unit> SetStrategyState(StrategyState state) =>
            State.SwapEff(s => SuccessEff(s.SetStrategyState(state)))
                 .Bind(_ => unitEff);
        
        /// <summary>
        /// Process an inbox message (tell)
        /// </summary>
        [Pure]
        public Aff<RT, InboxDirective> ProcessTell =>
            from __1 in ActorAff<RT>.assertNotCancelled()
            from req in ActorContextAff<RT>.Request
            from msg in preProcessMessageContent(req.CurrentMsg, "tell")
            from ins in AssertState
            from dir in (from ous in ActorFn(ins, msg)
                         from __2 in ProcessAff<RT>.catchAndLogErr(PublishNewState(ins, ous), unitEff)
                         from __3 in ResetStrategyState() 
                         select InboxDirective.Default)
                        .BiBind(SuccessAff, ActorAff<RT>.defaultErrorHandler)
            select dir;

        /// <summary>
        /// Process an inbox message (ask)
        /// </summary>
        [Pure]
        public Aff<RT, InboxDirective> ProcessAsk =>
            from __1 in ActorAff<RT>.assertNotCancelled()
            from req in ActorContextAff<RT>.Request
            from msg in preProcessMessageContent(req.CurrentRequest.Message, "ask")
            from res in ActorContextAff<RT>.localContext(req.SetCurrentMessage(msg),
                from ins in AssertState
                from dir in (from ous in ActorFn(ins, msg)
                             from __2 in ProcessAff<RT>.catchAndLogErr(PublishNewState(ins, ous), unitEff)
                             from __3 in ResetStrategyState()
                             select InboxDirective.Default)
                            .BiBind(SuccessAff, e => from _ in ProcessAff<RT>.catchAndLogErr(ProcessAff<RT>.replyError(e), unitEff)
                                                     from r in ActorAff<RT>.defaultErrorHandler(e)
                                                     select r)
                select dir)
            select res;

        /// <summary>
        /// A watched process has terminated, we have got the notification of that termination
        /// and will run the termination inbox
        /// </summary>
        [Pure]
        public Aff<RT, InboxDirective> ProcessTerminated(ProcessId tpid) =>
            TermFn == null
                ? SuccessEff(InboxDirective.Default)
                : from ins in AssertState
                  from dir in (from ous in TermFn(ins, tpid)
                               from __2 in ProcessAff<RT>.catchAndLogErr(PublishNewState(ins, ous), unitEff)
                               from __3 in ResetStrategyState() 
                               select InboxDirective.Default)
                              .BiBind(SuccessAff, ActorAff<RT>.defaultErrorHandler)
                  select dir;
        
        /// <summary>
        /// Hack to get around the fact that we want IActor to have no runtime attached
        /// TODO: Think about alternatives 
        /// </summary>
        public IActor<LRT> WithRuntime<LRT>() where LRT : struct, HasEcho<LRT> =>
            (IActor<LRT>) ((object) this);
    }

    static class ActorAff<RT> where RT : struct, HasEcho<RT>
    {
        public static Aff<RT, IActor> create<S, A>(
            bool cluster,
            ActorItem parent,
            ProcessName name,
            Func<S, A, Aff<RT, S>> actor,
            Func<IActor<RT>, Aff<RT, S>> setup,
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
        
        /// <summary>
        /// Wraps the user's shutdown function in a logging handler
        /// </summary>
        [Pure]
        static Func<S, Aff<RT, Unit>> loggingShutdown<S>(Func<S, Aff<RT, Unit>> shutdown) =>
            state =>
                ProcessAff<RT>.logErr<Unit>(shutdown(state));

        /// <summary>
        /// Inbox for system messages relating to the process
        /// </summary>
        [Pure]
        static Aff<RT, Unit> processSystemMessage(Message message) =>
            message.Tag switch
            {
                Message.TagSpec.GetChildren     => from cs in ProcessAff<RT>.Children
                                                   from __ in ProcessAff<RT>.replyIfAsked(cs)
                                                   select unit,
                Message.TagSpec.ShutdownProcess => from _ in shutdownProcess(false)
                                                   from r in ProcessAff<RT>.replyIfAsked(unit)
                                                   select unit,
                _ => unitEff
            };

        [Pure]
        public static Aff<RT, InboxDirective> defaultErrorHandler(Error e) =>
            defaultErrorHandler((Exception)e);

        [Pure]
        public static Aff<RT, InboxDirective> defaultErrorHandler(Exception e) =>
            from r in runStrategy(e)
            from _ in e is ProcessKillException pke
                               ? FailEff<Unit>(pke)
                               : ProcessAff<RT>.tell(ProcessAff<RT>.Errors, e)
            select r;

        [Pure]
        public static Aff<RT, InboxDirective> runStrategy(Error err) =>
            runStrategy((Exception) err);

        [Pure]
        public static Aff<RT, InboxDirective> runStrategy(Exception ex) =>
                (from message         in ActorContextAff<RT>.Request.Map(r => r.CurrentMsg)
                 from pid             in ProcessAff<RT>.Self
                 from parent          in ProcessAff<RT>.Parent
                 from sender          in ProcessAff<RT>.Sender
                 from siblings        in ActorContextAff<RT>.Siblings
                 from strategy        in ActorContextAff<RT>.ParentProcess.Map(p => p.Actor.Strategy)
                 from strategyState   in ActorContextAff<RT>.SelfProcess.Map(s => s.Actor.StrategyState)
                 from failureStrategy in SuccessEff(strategy.Failure(pid, parent, sender, siblings, ex, message))
                 from stratResult     in SuccessEff(failureStrategy.Run(strategyState))
                 from result          in stratResult.Value.Match(
                                            Some: decision => runStrategyOutcome(decision, ex),
                                            None: ()       => ProcessEff.logErr($"Strategy failed (no decision) in {pid}")
                                                                        .Map(_ => InboxDirective.Default),
                                            Fail: ex       => ProcessEff.logErr($"Strategy exception (decision error) {pid}", ex)
                                                                        .Map(_ => InboxDirective.Default))
                 select result)
                .Match(Succ: SuccessEff,
                       Fail: err => from p in ProcessAff<RT>.Self
                                    from _ in ProcessEff.logErr($"Strategy exception in {p}", err)
                                    select InboxDirective.Default)
                .Flatten();

        [Pure]
        static Aff<RT, InboxDirective> runStrategyOutcome(StrategyDecision decision, Exception ex) =>
                from strategyState in ActorContextAff<RT>.SelfProcess.Map(s => s.Actor.StrategyState)
                from pid           in ProcessAff<RT>.Self
                from sender        in ProcessAff<RT>.Sender
                from message       in ActorContextAff<RT>.Request.Map(r => r.CurrentMsg)
                from ctx           in ActorContextAff<RT>.SelfProcess
                from ___           in ctx.Actor.SetStrategyState(strategyState)
                
                // Run the process directive
                from dr1 in decision.ProcessDirective.Type != DirectiveType.Stop && decision.Pause > 0 * seconds
                                  // Tell all other sibling processes to pause 
                                ? from __1 in decision.Affects.Map(ProcessAff<RT>.pause).SequenceParallel()
                                  
                                  // Delay the running of the process directive 
                                  from __2 in (from _ in IO.Time.sleepFor<RT>(decision.Pause)
                                               from d in runProcessDirective(pid, sender, ex, message, decision, true)
                                               select d)
                                              .FireAndForget()
                                  select InboxDirective.Pause
                                  
                                  // Not pausing, so let's run the process directive straight away
                                : runProcessDirective(pid, sender, ex, message, decision, true).Map(_ => InboxDirective.Default)
                 
                // Run the message directive
                from dr2 in runMessageDirective(pid, sender, decision, ex, message)
                select dr1 | dr2;

        [Pure]
        public static Aff<RT, InboxDirective> runMessageDirective(ProcessId pid, ProcessId sender, StrategyDecision decision, Exception e, object message) => 
                decision.MessageDirective switch {
                    
                                            // Send the failed message to our parent
                    ForwardToParent _ =>    from _ in ProcessAff<RT>.tell(pid.Parent, message, sender)
                                            select InboxDirective.Default,
                    
                                            // Send the failed message to the back of our queue
                    ForwardToSelf _ =>      from _ in ProcessAff<RT>.tell(pid, message, sender)
                                            select InboxDirective.Default,
                    
                                            // Send the failed message to another specified process
                    ForwardToProcess ftp => from _ in ProcessAff<RT>.tell(ftp.ProcessId, message, sender)
                                            select InboxDirective.Default,
                    
                                            // Leave it in the queue, this is like putting it to the front of the queue
                                            // because we never removed it in the first place
                    StayInQueue _ =>        SuccessEff(InboxDirective.PushToFrontOfQueue),
                    
                                            // Send to dead letters
                    _ =>                    e switch {
                                                  ProcessKillException _ =>
                                                      from _ in ProcessAff<RT>.tell(ProcessAff<RT>.DeadLetters, DeadLetter.create(sender, pid, e, "Process error: ", message))
                                                      select InboxDirective.Default,
                                                  
                                                   _ => SuccessEff(InboxDirective.Default)
                                            }
                };

        public static Aff<RT, Unit> runProcessDirective(
            ProcessId pid,
            ProcessId sender,
            Exception e,
            object message,
            StrategyDecision decision,
            bool unPauseAfterRestart) =>
                from _1 in decision.Affects
                                   .Filter(x => x != pid)
                                   .Map(cpid => decision.ProcessDirective switch {
                                                    Escalate _ => ProcessAff<RT>.unpause(cpid),
                                                    Resume _   => ProcessAff<RT>.unpause(cpid),
                                                    Restart _  => ProcessAff<RT>.restart(cpid),
                                                    Stop _     => ProcessAff<RT>.kill(cpid),
                                                    _          => unitEff
                                    })
                                   .SequenceParallel()
                            
                from _2 in decision.ProcessDirective switch {
                                Escalate _ => ProcessAff<RT>.tellSystem(ProcessAff<RT>.Parent, SystemMessage.ChildFaulted(pid, sender, e, message)),
                                Resume _   => ProcessAff<RT>.unpause(pid),
                                Restart _  => from slf in ActorContextAff<RT>.SelfProcess
                                              from ___ in slf.Actor.WithRuntime<RT>().Restart(unPauseAfterRestart)
                                              select unit,
                                Stop _     => shutdownProcess(false),
                                _          => unitEff
                           }
                select unit;


        public static Aff<RT, Unit> shutdownProcess(bool maintainState) =>
            from sys in ActorContextAff<RT>.LocalSystem
            from req in ActorContextAff<RT>.Request
            from slf in SuccessEff(req.Self)
            from par in SuccessEff(req.Parent)
            from shi in ActorSystemAff<RT>.InboxShutdownItem
            from shu in shutdownProcessRec(slf, par, (ILocalActorInbox)shi.Inbox, maintainState)
            select unit;    

        static Aff<RT, Unit> shutdownProcessRec(ActorItem item, ActorItem parent, ILocalActorInbox shutdownProcess, bool maintainState) =>
            from shu in item.Actor.Children
                                  .Values
                                  .Map(child => shutdownProcessRec(child, item, shutdownProcess, maintainState))
                                  .SequenceParallel()
            from unl in parent.Actor.UnlinkChild(item.Actor.Id)
            from dis in shutdownProcess.Tell<RT>(item.Inbox, ProcessId.NoSender, None) 
            from res in item.Actor.WithRuntime<RT>().Shutdown(maintainState)
            select res;

        public static Eff<RT, Unit> assertNotCancelled() =>
            from c in Prelude.cancelToken<RT>().Map(t => t.IsCancellationRequested)
            from r in c ? FailEff<Unit>(Error.New("Cancelled")) : unitEff
            select r;

        public static Aff<RT, A> writeDeadLetter<A>(string letter, object message) =>
            from slf in ProcessAff<RT>.Self
            from sys in ActorContextAff<RT>.LocalSystem
            from sdr in ProcessAff<RT>.Sender
            from res in ActorSystemAff<RT>.tell(
                            sys.DeadLetters,
                            DeadLetter.create(sdr, slf, letter, message),
                            Schedule.Immediate,
                            sdr)
            from rep in ProcessAff<RT>.isAsk.Bind(f => f ? ProcessAff<RT>.replyError(new AskException($"Ask failed: {letter}")) : unitEff)
            from fai in FailEff<A>(Error.New(letter))  
            select fai;
    }
}
