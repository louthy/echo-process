using System;
using Echo.Traits;
using LanguageExt;
using LanguageExt.Sys;
using System.Threading;
using LanguageExt.Pipes;
using LanguageExt.Common;
using LanguageExt.Sys.Traits;
using LanguageExt.TypeClasses;
using System.Reactive.Subjects;
using System.Threading.Channels;
using LanguageExt.ClassInstances;
using System.Collections.Generic;
using static LanguageExt.Prelude;
using static LanguageExt.Pipes.Proxy;

namespace Echo.ActorSys2
{
    /// <summary>
    /// Reference to an actor
    /// </summary>
    internal record Actor<RT>(
        ProcessName Name,
        Func<UserPost, Eff<Unit>> User,
        Func<SysPost, Eff<Unit>> Sys,
        Aff<RT, Unit> Effect,
        ActorState<RT> State)
        where RT : struct, HasEcho<RT>
    {
        public static readonly Actor<RT> None = new Actor<RT>(
            default, 
            _ => unitEff, 
            _ => unitEff, 
            unitEff, 
            ActorState<RT>.None);

        public bool IsNone =>
            !Name.IsValid;
    }

    /// <summary>
    /// Actor that manages the life-type of a Process 
    /// </summary>
    static class Actor<RT, EqS, S, A>
        where RT : struct, HasEcho<RT>, HasTime<RT>
        where EqS : struct, Eq<S>
    {
        static readonly Eff<Unit> AlreadyCancelled = unitEff; 
        const int SysInboxSize = 100;

        /// <summary>
        /// Internal state of the actor
        /// </summary>
        record ActorState(
                ProcessId Self,
                ProcessId Parent,
                HashMap<ProcessName, Actor<RT>> Children,
                Option<S> State,
                Subject<object> Publish,
                Aff<RT, S> Setup,
                Func<S, A, Aff<RT, S>> Inbox,
                Func<S, Aff<RT, Unit>> Shutdown,
                Func<S, ProcessId, Aff<RT, S>> Terminated,
                Consumer<RT, S, Unit> StateChanged,
                State<StrategyContext, Unit> Strategy,
                StrategyState StrategyState,
                Eff<Unit> CancelInbox,
                Channel<UserPost> UserChannel,
                Channel<SysPost> SysChannel)
            : Echo.ActorSys2.ActorState<RT>(Self, Parent, Children, Strategy);

        /// <summary>
        /// Make an actor
        /// </summary>
        /// <param name="self">The process ID of this actor</param>
        /// <param name="parent">The process ID of the parent</param>
        /// <param name="setup">Process setup function</param>
        /// <param name="inbox">Process inbox function</param>
        /// <param name="shutdown">Process shutdown function</param>
        /// <param name="terminated">If this process is watching another process then when that other process dies this
        /// function is called.  It allows one process to monitor the liveness of another</param>
        /// <param name="stateChanged">Invoked when the state changes - allows for persistence hooks and publish streams</param>
        /// <param name="maxInboxSize">The maximum queue size</param>
        /// <param name="strategy">Strategy to run when a child process faults</param>
       /// <returns></returns>
        public static Actor<RT> make(
            ProcessId self,
            ProcessId parent,
            Aff<RT, S> setup,
            Func<S, A, Aff<RT, S>> inbox,
            Func<S, Aff<RT, Unit>> shutdown,
            Func<S, ProcessId, Aff<RT, S>> terminated,
            Consumer<RT, S, Unit> stateChanged, 
            int maxInboxSize,
            State<StrategyContext, Unit> strategy)
        {
            var userChannel = makeChannel<UserPost>(maxInboxSize);
            var sysChannel  = makeChannel<SysPost>(SysInboxSize);

            var state = Atom<Echo.ActorSys2.ActorState<RT>>(
                new ActorState(
                    Self: self,
                    Parent: parent,
                    Children: Empty,
                    State: None,
                    Publish: new(),
                    Setup: setup,
                    Inbox: inbox,
                    Shutdown: shutdown,
                    Terminated: terminated,
                    StateChanged: stateChanged,
                    Strategy: strategy,
                    StrategyState: StrategyState.Empty,
                    CancelInbox: AlreadyCancelled,
                    UserChannel: userChannel,
                    SysChannel: sysChannel));

            return new Actor<RT>(self.Name, 
                                 msg => writeToChannel(userChannel, msg), 
                                 msg => writeToChannel(sysChannel, msg),
                                 local(makeProcessEffect.RunEffect(), state),
                                 state);
        }

        /// <summary>
        /// Create an effect that represents the entire life of the Process
        /// </summary>
        /// <returns>And effect represents the entire Process</returns>
        static Effect<RT, Unit> makeProcessEffect =>
            startup | sysChannelPipe | processSysMessage;

        /// <summary>
        /// Startup producer
        /// </summary>
        /// <remarks>
        /// Creates a effect for the user messages forks it (so it runs independently of the system inbox).
        /// Then runs the setup function (knowing the inbox is ready to receive messages)
        /// Puts the state into the local context
        /// Then yields a new channel for receiving system-messages 
        /// </remarks>
        /// <returns>A producer that producers the system-channel, this is yielded post setup of the inbox and running
        /// of the setup function</returns>
        static Producer<RT, Channel<SysPost>, Unit> startup =>
            from ch in getSysChannel
            from _1 in startUserInbox
            from _2 in userSetup
            from _3 in yield(ch)
            select unit;

        /// <summary>
        /// Run the user setup function
        /// </summary>
        static Aff<RT, S> userSetup =>
            from state in setup
            from _     in putState(state)
            select state;

        /// <summary>
        /// Creates an effect that represents the user-inbox.
        /// </summary>
        /// <remarks>
        /// The effect is then forked so that it can run independently of the system-inbox.  This also allows it to be
        /// cancelled (when there's errors, or pause requests)
        /// </remarks>
        static Eff<RT, Unit> startUserInbox =>
            from u in getUserChannel
            from x in cancelInbox
            from c in fork(u | (channelPipe<UserPost>() | processUserMessage))
            from _ in putCancel(c)
            select unit;

        /// <summary>
        /// Consumer that awaits a UserPost and any other related context.
        /// If the UserPost message is of the correct type for this Process then it attempts to pass it to the inbox
        /// function.  Otherwise it passes it on to the dead-letters Process 
        /// </summary>
        static Consumer<RT, UserPost, Unit> processUserMessage =>
            from p in awaiting<UserPost>()
            from x in p.Message switch
                      {
                          A msg => runUserMessage(msg, p) | catchInbox(p),
                          _     => Process<RT>.forwardToDeadLetters(p)
                      }
            select unit;

        /// <summary>
        /// User space error handler
        /// </summary>
        static AffCatch<RT, Unit> catchInbox(UserPost post) =>
            @catch(e => from s in getSelf
                        from _ in Process<RT>.tellParentSystem(new ChildFaultedSysPost(e, post, s))
                        from x in cancel<RT>()
                        select unit);

        /// <summary>
        /// Processes a user-message
        /// </summary>
        /// <remarks>
        /// Passes the message to the user's inbox function.  If the resulting state is equal to what went it, nothing
        /// changes in the Actor.  Otherwise we update the state and publish it.
        ///
        /// If the inbox function fails then we tell the parent we're broken and cancel this forked inbox. 
        /// </remarks>
        static Aff<RT, Unit> runUserMessage(A msg, UserPost post) =>
            from ib in getUserInbox
            from os in getState | userSetup
            from ns in ib(os, msg)
            from _1 in putState(ns)
            from _2 in isStateEqual(os, ns) ? SuccessEff(unit) : publishState(ns)
            select unit;

        /// <summary>
        /// State equality
        /// </summary>
        static bool isStateEqual(S o, S s) =>
            TypeClass.equals<EqS, S>(o, s);

        /// <summary>
        /// Consumer of SysPost messages that control the life-cycle of the Process
        /// </summary>
        static Consumer<RT, SysPost, Unit> processSysMessage =>
            from p in awaiting<SysPost>()
            from _ in p switch
                      {
                          ChildFaultedSysPost(var error, var cpost, var child)    => childFaulted(error, cpost, child),
                          LinkChildSysPost<RT>(var actor, var sender)             => linkChild(actor),
                          ShutdownProcessSysPost(var maintainState, var sender) m => shutdownProcess(m),
                          RestartSysPost(var pauseForMilliseconds, var sender)    => restart(pauseForMilliseconds),
                          ResumeSysPost(var pauseForMilliseconds, var sender)     => resume(pauseForMilliseconds),
                          PauseSysPost(var sender)                                => pause,
                          UnpauseSysPost(var sender)                              => unpause,
                          WatchSysPost(var pid)                                   => watch(pid),
                          UnWatchSysPost(var pid)                                 => unwatch(pid),
                          _                                                       => Process<RT>.forwardToDeadLetters(p)
                      } | logSysError
            select unit;

        /// <summary>
        /// When a new actor is created, this is called to make it a child of this actor 
        /// </summary>
        static Aff<RT, Unit> linkChild(Actor<RT> actor) =>
            from kids in getChildren
            
            // Make sure it doesn't already exist
            from _1 in kids.ContainsKey(actor.Name)
                           ? getSelf.Bind(s => FailEff<Unit>(ProcessError.ChildAlreadyExists(s, actor.Name)))
                           : unitEff
                             
            // Link the actor as a child of this one
            from _2 in modifyChildren(ks => ks.Add(actor.Name, actor))
            
            // Run the child in a forked computation
            from _3 in fork(actor.Effect)
            select unit;
        
        /// <summary>
        /// Handles a child that has faulted
        /// </summary>
        /// <remarks>
        /// Runs the strategy attached to this process (which is the parent of the one that faulted).  The result
        /// is two directives: Inbox and Message.  The Inbox directive decides the fate of the child-process (or the
        /// child-processes if it's an all-for-one strategy).  The Message directive decides where the message that
        /// caused the fault should be directed.
        ///
        /// The default Inbox directive is to restart the process, which means the setup function runs again and then
        /// continues processing the inbox.
        ///
        /// The default Message directive is to forward to the dead-letters process. 
        /// </remarks>
        /// <param name="error">Error representing the fault</param>
        /// <param name="post">The UserPost that caused the fault</param>
        /// <param name="child">The Process that experienced the fault</param>
        static Aff<RT, Unit> childFaulted(Error error, UserPost post, ProcessId child) =>
            from strat   in getStrategy
            from state   in getStrategyState 
            from self    in getSelf
            from kids    in getChildren.Map(c => c.Keys.Map(child.Parent.Child))
            
            // Run the strategy
            from outcome in Eff(() => strat.Run(StrategyContext.Empty.With(
                                                    Global: state,
                                                    Exception: (Exception)error,
                                                    Message: post.Message,
                                                    Sender: post.Sender,
                                                    FailedProcess: child,
                                                    ParentProcess: child.Parent,
                                                    Siblings: kids)))
            
            // Deal with the child-processes and what action to take 
            from _1 in outcome.State.Directive.Case switch
                       {
                           Resume   => Process<RT>.postMany(outcome.State.Affects, new ResumeSysPost((int)outcome.State.Pause.Milliseconds, self)),
                           Restart  => Process<RT>.postMany(outcome.State.Affects, new RestartSysPost((int)outcome.State.Pause.Milliseconds, self)),
                           Stop     => Process<RT>.postMany(outcome.State.Affects, new ShutdownProcessSysPost(false, self)),
                           Escalate => Process<RT>.postMany(outcome.State.Affects, new ChildFaultedSysPost(error, post, child)),
                           _        => unitEff
                       }

            // Follow the directive to handle the failed message
            from _2 in outcome.State.MessageDirective.Case switch
                       {
                           ForwardToSelf        => Process<RT>.post(child, post), 
                           ForwardToParent      => Process<RT>.post(self, post), 
                           ForwardToProcess ftp => Process<RT>.post(ftp.ProcessId, post), 
                           StayInQueue          => Process<RT>.post(child, post), // TODO: This will need some thought 
                           _                    => Process<RT>.forwardToDeadLetters(post)
                       }
            
            from _3 in modify(a => a with { StrategyState = outcome.State.Global })
            
            select unit;

        /// <summary>
        /// Shutdown this process and its child processes
        /// </summary>
        static Aff<RT, Unit> shutdownProcess(ShutdownProcessSysPost msg) =>

            // Stop the user-inbox from doing anything else
            from _1 in cancelInbox

            // Kill the child processes
            from children in getChildren
            from _2       in putChildren(Empty)
            from _3       in children.Values.Sequence(c => c.Sys(msg))

            // Persist the state 
            from _4 in msg.MaintainState
                           ? saveState
                           : unitEff
            
            // Kill any subscriptions
            from _5 in publishComplete 
                   
            // Run the user shutdown
            from state    in getState
            from shutdown in getShutdown
            from _7       in shutdown(state) | logError // TODO: Log error

            select unit;
        
        static Aff<RT, Unit> saveState =>
            from s in getState
            // TODO: Decide about persistence
            
            select unit;

        /// <summary>
        /// Restart the processing of the user messages but before we do we re-run the user's setup
        /// function to get some new state 
        /// </summary>
        /// <param name="pauseForMilliseconds">Pre-pause before resuming</param>
        static Aff<RT, Unit> restart(int pauseForMilliseconds) =>
            from _ in Time<RT>.sleepFor(TimeSpan.FromMilliseconds(pauseForMilliseconds))
            from s in userSetup
            from x in startUserInbox 
            select unit;

        /// <summary>
        /// Resume the processing of the user messages without destroying the user-state 
        /// </summary>
        /// <param name="pauseForMilliseconds">Pre-pause before resuming</param>
        static Aff<RT, Unit> resume(int pauseForMilliseconds) =>
            Time<RT>.sleepFor(TimeSpan.FromMilliseconds(pauseForMilliseconds))
                    .Bind(static _ => startUserInbox); 

        /// <summary>
        /// Pause the inbox
        /// </summary>
        /// <remarks>
        /// This shuts down the effect so nothing else is consumed until we're un-paused
        /// </remarks>
        static Aff<RT, Unit> pause =>
            cancelInbox;

        /// <summary>
        /// Un-pause the inbox
        /// </summary>
        /// <remarks>
        /// This restarts the inbox
        /// </remarks>
        static Aff<RT, Unit> unpause =>
            startUserInbox;

        /// <summary>
        /// Watch the `watched` process for termination, then run the termination-inbox in self 
        /// </summary>
        static Aff<RT, Unit> watch(ProcessId watched) =>
            getSelf.Bind(s => Process<RT>.watch(s, watched));

        /// <summary>
        /// Un-watch the `watched` process for termination 
        /// </summary>
        static Aff<RT, Unit> unwatch(ProcessId watched) =>
            getSelf.Bind(s => Process<RT>.unwatch(s, watched));

        /// <summary>
        /// Create a context within the runtime that belongs entirely to this Actor
        /// </summary>
        static Aff<RT, X> local<X>(Aff<RT, X> ma, Atom<Echo.ActorSys2.ActorState<RT>> state) =>
            localAff<RT, RT, X>(rt => rt.LocalCancel.LocalEcho(e => e.LocalEcho(state)), ma);

        /// <summary>
        /// Get the actor's state
        /// </summary>
        static Eff<RT, ActorState> get =>
            #nullable disable
            runtime<RT>().Map(static rt => (ActorState)rt.EchoState.GetCurrentActorState());
            #nullable enable

        /// <summary>
        /// Get the user's state
        /// </summary>
        static Eff<RT, S> getState =>
            get.Bind(static a => a.State.Case switch
                                 {
                                     S x => SuccessEff(x),
                                     _   => FailEff<S>(ProcessError.StateNotSet)
                                 });

        /// <summary>
        /// Get the strategy computation
        /// </summary>
        static Eff<RT, State<StrategyContext, Unit>> getStrategy =>
            get.Map(static a => a.Strategy);

        /// <summary>
        /// Get the long running strategy state
        /// </summary>
        /// <remarks>
        /// This carries over restarts so we can track back-off and max-retries
        /// </remarks>
        static Eff<RT, StrategyState> getStrategyState =>
            get.Map(static a => a.StrategyState);

        /// <summary>
        /// Get the ProcessId of this Actor
        /// </summary>
        static Eff<RT, ProcessId> getSelf =>
            get.Map(static a => a.Self);

        /// <summary>
        /// Get the ProcessId of this Actor
        /// </summary>
        static Eff<RT, ProcessId> getParent =>
            get.Map(static a => a.Parent);

        /// <summary>
        /// Children of this Actor
        /// </summary>
        static Eff<RT, HashMap<ProcessName, Actor<RT>>> getChildren =>
            get.Map(static a => a.Children);

        /// <summary>
        /// Get the shutdown function
        /// </summary>
        static Eff<RT, Func<S, Aff<RT, Unit>>> getShutdown =>
            get.Map(static a => a.Shutdown);

        /// <summary>
        /// Get the terminated function
        /// </summary>
        static Eff<RT, Func<S, ProcessId, Aff<RT, S>>> getTerminated =>
            get.Map(static a => a.Terminated);

        /// <summary>
        /// Get the user inbox function
        /// </summary>
        static Eff<RT, Func<S, A, Aff<RT, S>>> getUserInbox =>
            get.Map(static a => a.Inbox);

        /// <summary>
        /// Get the user setup function
        /// </summary>
        static Aff<RT, S> setup =>
            get.Bind(static a => a.Setup.Clone());

        /// <summary>
        /// Get the user channel
        /// </summary>
        static Eff<RT, Channel<UserPost>> getUserChannel =>
            get.Map(static a => a.UserChannel);

        /// <summary>
        /// Get the system channel
        /// </summary>
        static Eff<RT, Channel<SysPost>> getSysChannel =>
            get.Map(static a => a.SysChannel);

        /// <summary>
        /// Write state
        /// </summary>
        static Eff<RT, Unit> put(ActorState state) =>
            runtime<RT>().Map(rt => ignore(rt.EchoState.ModifyCurrentActorState(_ => state)));

        /// <summary>
        /// Write user state
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        static Eff<RT, Unit> putState(S state) =>
            modify(a => a with {State = state});

        /// <summary>
        /// Atomically modify the actor's state 
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        static Eff<RT, Unit> modify(Func<ActorState, ActorState> f) =>
            runtime<RT>().Map(rt => ignore(rt.EchoState.ModifyCurrentActorState(s => f((ActorState) s))));

        /// <summary>
        /// Atomically modify the actor's children
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        static Eff<RT, Unit> modifyChildren(Func<HashMap<ProcessName, Actor<RT>>, HashMap<ProcessName, Actor<RT>>> f) =>
            modify(a => a with {Children = f(a.Children)});

        /// <summary>
        /// Put a new map of children into the state 
        /// </summary>
        static Eff<RT, Unit> putChildren(HashMap<ProcessName, Actor<RT>> children) =>
            modify(a => a with {Children = children});

        /// <summary>
        /// Write a new inbox cancellation effect into the state
        /// </summary>
        static Eff<RT, Unit> putCancel(Eff<Unit> cancel) =>
            modify(a => a with {CancelInbox = cancel});

        /// <summary>
        /// Cancel the user inbox for this process
        /// </summary>
        static Eff<RT, Unit> cancelInbox =>
            from s in get
            from x in s.CancelInbox
            from _ in putCancel(AlreadyCancelled)
            select unit;

        /// <summary>
        /// Publish state
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        static Aff<RT, Unit> publishState(S state) =>
            #nullable disable
            get.Bind(a => (state | a.StateChanged).RunEffect()) | logError;
            #nullable enable

        /// <summary>
        /// Publish a value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static Eff<RT, Unit> publish(object value) =>
            get.Map(a => { a.Publish.OnNext(value); return unit; }) | logError;

        /// <summary>
        /// Shutdown the publish stream
        /// </summary>
        static Eff<RT, Unit> publishComplete =>
            get.Map(a => { a.Publish.OnCompleted(); return unit; }) | logError;

        /// <summary>
        /// Catch an error and log it
        /// </summary>
        static readonly EffCatch<RT, Unit> logError =
            @catch(Process<RT>.logErr);

        /// <summary>
        /// Catch an error and log it
        /// </summary>
        static readonly EffCatch<RT, Unit> logSysError =
            @catch(Process<RT>.logSysErr);

        /// <summary>
        /// Create a bounded channel that represents an inbox
        /// </summary>
        static Channel<X> makeChannel<X>(int capacity) =>
            Channel.CreateBounded<X>(capacity);

        /// <summary>
        /// Make a pipe that accepts a channel and then iterates over everything in the channel yielding the items
        /// </summary>
        static Pipe<RT, Channel<X>, X, Unit> channelPipe<X>() =>
            from c in Pipe.awaiting<RT, Channel<X>, X>()
            from r in Pipe.lift<RT, Channel<X>, X, RT>(runtime<RT>())
            from _ in Pipe.enumerate<RT, Channel<X>, X>(channelYield(c, r))
            select unit;
        
        /// <summary>
        /// Make a pipe that accepts a SysPost channel and then iterates over everything in the channel yielding the items
        /// </summary>
        /// <remarks>
        /// This is different to the channelPipe is that it will end if a ShutdownProcessSysPost comes through
        /// </remarks>
        static Pipe<RT, Channel<SysPost>, SysPost, Unit> sysChannelPipe =>
            from c in Pipe.awaiting<RT, Channel<SysPost>, SysPost>()
            from r in Pipe.lift<RT, Channel<SysPost>, SysPost, RT>(runtime<RT>())
            from _ in Pipe.enumerate<RT, Channel<SysPost>, SysPost>(sysChannelYield(c, r))
            select unit;
        
        /// <summary>
        /// Turn a channel into an IAsyncEnumerable
        /// </summary>
        static async IAsyncEnumerable<X> channelYield<X>(Channel<X> channel, RT runtime)
        {
            while (!runtime.CancellationToken.IsCancellationRequested && 
                   await channel.Reader.WaitToReadAsync(runtime.CancellationToken).ConfigureAwait(false))
            {
                yield return await channel.Reader.ReadAsync(runtime.CancellationToken).ConfigureAwait(false);
            }
        }
        
        /// <summary>
        /// Turn a channel into an IAsyncEnumerable
        /// </summary>
        static async IAsyncEnumerable<SysPost> sysChannelYield(Channel<SysPost> channel, RT runtime)
        {
            while (!runtime.CancellationToken.IsCancellationRequested && 
                   await channel.Reader.WaitToReadAsync(runtime.CancellationToken).ConfigureAwait(false))
            {
                var post = await channel.Reader.ReadAsync(runtime.CancellationToken).ConfigureAwait(false);
                yield return post;
                if (post is ShutdownProcessSysPost) break;
            }
        }

        /// <summary>
        /// Post something into a channel
        /// </summary>
        static Eff<Unit> writeToChannel<X>(Channel<X> channel, X value) =>
            EffMaybe<Unit>(
                () => channel.Writer.TryWrite(value)
                          ? unit
                          : ProcessError.QueueFull);
    }
}