using LanguageExt;
using LanguageExt.UnitsOfMeasure;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Echo.Traits;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;

namespace Echo
{
    /// <summary>
    /// <para>
    ///     The Language Ext process system uses the actor model as seen in Erlang 
    ///     processes.  Actors are famed for their ability to support massive concurrency
    ///     through messaging and no shared memory.
    /// </para>
    /// <para>
    ///     https://en.wikipedia.org/wiki/Actor_model
    /// </para>
    /// <para>
    ///     Each process has an 'inbox' and a state.  The state is the property of the
    ///     process and no other.  The messages in the inbox are passed to the process
    ///     one at a time.  When the process has finished processing a message it returns
    ///     its current state.  This state is then passed back in with the next message.
    /// </para>
    /// <para>
    ///     You can think of it as a fold over a stream of messages.
    /// </para>
    /// <para>
    ///     A process must finish dealing with a message before another will be given.  
    ///     Therefore they are blocking.  But they block themselves only. The messages 
    ///     will build up whilst they are processing.
    /// </para>
    /// <para>
    ///     Because of this, processes are also in a 'supervision hierarchy'.  Essentially
    ///     each process can spawn child-processes and the parent process 'owns' the child.  
    /// </para>
    /// <para>
    ///     Processes have a default failure strategy where the process just restarts with
    ///     its original state.  The inbox always survives a crash and the failed message 
    ///     is sent to a 'dead letters' process.  You can monitor this. You can also provide
    ///     bespoke strategies for different types of failure behaviours (See Strategy folder)
    /// </para>
    /// <para>
    ///     So post crash the process restarts and continues processing the next message.
    /// </para>
    /// <para>
    ///     By creating child processes it's possible for a parent process to 'offload'
    ///     work.  It could create 10 child processes, and simply route the messages it
    ///     gets to its children for a very simple load balancer. Processes are very 
    ///     lightweight and should not be seen as Threads or Tasks.  You can create 
    ///     10s of 1000s of them and it will 'just work'.
    /// </para>
    /// <para>
    ///     Scheduled tasks become very simple also.  You can send a process to a message
    ///     with a delay.  So a background process that needs to run every 30 minutes 
    ///     can just send itself a message with a delay on it at the end of its message
    ///     handler:
    /// </para>
    /// <para>
    ///         tellSelf(unit, TimeSpan.FromMinutes(30));
    /// </para>
    /// </summary>
    public static partial class Process<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        internal static Eff<RT, EchoState<RT>> echoState =>
            Eff<RT, EchoState<RT>>(rt => rt.EchoState);

        /// <summary>
        /// Run the Aff within a system context
        /// </summary>
        public static Aff<RT, A> withSystem<A>(SystemName name, Aff<RT, A> ma) =>
            localAff<RT, RT, A>(rt => rt.MapEchoState(es => es.WithSystem(name)), ma);
        
        /// <summary>
        /// Triggers when the Process system shuts down
        /// Either subscribe to the OnNext or OnCompleted
        /// </summary>
        public static IObservable<ShutdownCancellationToken> PreShutdown =>
            Process.PreShutdown;

        /// <summary>
        /// Triggers when the Process system shuts down
        /// Either subscribe to the OnNext or OnCompleted
        /// </summary>
        public static IObservable<SystemName> Shutdown =>
            Process.Shutdown;

        /// <summary>
        /// Log of everything that's going on in the Languge Ext process system
        /// </summary>
        public static IObservable<ProcessLogItem> ProcessSystemLog => 
            Process.ProcessSystemLog;

        /// <summary>
        /// Current process ID
        /// </summary>
        /// <remarks>
        /// This should be used from within a process message loop only
        /// </remarks>
        public static Eff<RT, ProcessId> Self =>
            echoState.Map(static es => es.Self);

        /// <summary>
        /// Parent process ID
        /// </summary>
        /// <remarks>
        /// This should be used from within a process message loop only
        /// </remarks>
        public static Eff<RT, ProcessId> Parent =>
            echoState.Map(static es => es.InMessageLoop
                                           ? es.Request.Parent.Actor.Id
                                           : Process.raiseUseInMsgLoopOnlyException<ProcessId>(nameof(Parent)));

        /// <summary>
        /// Root process ID
        /// The Root process is the parent of all processes
        /// </summary>
        public static Eff<RT, ProcessId> Root =>
            echoState.Map(static es => es.System.Root);

        /// <summary>
        /// User process ID
        /// The User process is the default entry process, your first process spawned
        /// will be a child of this process.
        /// </summary>
        public static Eff<RT, ProcessId> User =>
            echoState.Map(static es => es.System.User);

        /// <summary>
        /// Dead letters process
        /// Subscribe to it to monitor the failed messages (<see cref="subscribe(ProcessId)"/>)
        /// </summary>
        public static Eff<RT, ProcessId> DeadLetters =>
            echoState.Map(static es => es.System.DeadLetters);

        /// <summary>
        /// Errors process
        /// Subscribe to it to monitor the errors thrown 
        /// </summary>
        public static Eff<RT, ProcessId> Errors  =>
            echoState.Map(static es => es.System.Errors);

        /// <summary>
        /// Sender process ID
        /// Always valid even if there's not a sender (the 'NoSender' process ID will
        /// be provided).
        /// </summary>
        public static Eff<RT, ProcessId> Sender =>
            echoState.Map(static es => es.Request.Sender);

        /// <summary>
        /// Get the child processes of the running process
        /// </summary>
        /// <remarks>
        /// This should be used from within a process message loop only
        /// </remarks>
        public static Eff<RT, HashMap<string, ProcessId>> Children =>
            echoState.Map(static es => es.InMessageLoop
                                           ? es.Request.Children
                                           : Process.raiseUseInMsgLoopOnlyException<HashMap<string,ProcessId>>(nameof(Children)));

        /// <summary>
        /// Get the child processes of the running process
        /// </summary>
        /// <remarks>
        /// This should be used from within a process message loop only
        /// </remarks>
        public static Eff<RT, Map<string, ProcessId>> ChildrenInOrder =>
            Children.Map(static hm => hm.ToMap());

        /// <summary>
        /// Get the child processes of the process ID provided
        /// </summary>
        public static Aff<RT, HashMap<string, ProcessId>> children(ProcessId pid) =>
            echoState.Map(es => es.GetSystem(pid).GetChildren(pid));

        /// <summary>
        /// Get the child processes of the process ID provided
        /// </summary>
        public static Aff<RT, HashMap<string, ProcessId>> children(Eff<RT, ProcessId> pid) =>
            pid.Bind(p => echoState.Map(es => es.GetSystem(p).GetChildren(p)));

        /// <summary>
        /// Get the child processes of the process ID provided
        /// </summary>
        public static Aff<RT, Map<string, ProcessId>> childrenInOrder(ProcessId pid) =>
            children(pid).Map(static hm => hm.ToMap());

        /// <summary>
        /// Get the child processes of the process ID provided
        /// </summary>
        public static Aff<RT, Map<string, ProcessId>> childrenInOrder(Eff<RT, ProcessId> pid) =>
            children(pid).Map(static hm => hm.ToMap());

        /// <summary>
        /// Get the child processes by name
        /// </summary>
        public static Eff<RT, ProcessId> child(ProcessName name) =>
            echoState.Map(es => es.InMessageLoop
                                    ? es.Self[name]
                                    : Process.raiseUseInMsgLoopOnlyException<ProcessId>(nameof(child)));

        /// <summary>
        /// Get the child processes by index.
        /// </summary>
        /// <remarks>
        /// Because of the potential changeable nature of child nodes, this will
        /// take the index and mod it by the number of children.  We expect this 
        /// call will mostly be used for load balancing, and round-robin type 
        /// behaviour, so feel that's acceptable.  
        /// </remarks>
        public static Eff<RT, ProcessId> child(int index) =>
            echoState.Map(es => es.InMessageLoop
                                    ? es.Request.Children.Count == 0
                                          ? raise<ProcessId>(new NoChildProcessesException())
                                          : es.Request
                                              .Children
                                              .Values
                                              .Skip(index % ActorContext.Request.Children.Count)
                                              .Head()
                                    : Process.raiseUseInMsgLoopOnlyException<ProcessId>(nameof(child)));

        /// <summary>
        /// Immediately kills the Process that is running from within its message
        /// loop.  It does this by throwing a ProcessKillException which is caught
        /// and triggers the shutdown of the Process.  Any Process that has a 
        /// persistent inbox or state will also have its persistent data wiped.  
        /// If the Process is registered it will have its registration revoked.
        /// If you wish for the data to be maintained for future spawns then call 
        /// Process.shutdown()
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Eff<RT, Unit> killSelf =>
            echoState.Map(es => es.InMessageLoop
                                    ? raise<Unit>(new ProcessKillException())
                                    : Process.raiseUseInMsgLoopOnlyException<Unit>(nameof(kill)));

        /// <summary>
        /// Shutdown the currently running process.  The shutdown message jumps 
        /// ahead of any messages already in the process's queue but doesn't exit
        /// immediately like kill().  Any Process that has a persistent inbox or 
        /// state will have its state maintained for future spawns.  If you wish 
        /// for the data to be dropped then call Process.kill()
        /// </summary>
        public static Aff<RT, Unit> shutdownSelf =>
            Self.Bind(shutdown);

        /// <summary>
        /// Kill a specified running process.
        /// Forces the specified Process to shutdown.  The kill message jumps 
        /// ahead of any messages already in the process's queue.  Any Process
        /// that has a persistent inbox or state will also have its persistent
        /// data wiped.  If the Process is registered it will also its 
        /// registration revoked.
        /// If you wish for the data to be maintained for future
        /// spawns then call Process.shutdown(pid);
        /// </summary>
        public static Aff<RT, Unit> kill(ProcessId pid) =>
            Eff(() => Process.kill(pid));

        /// <summary>
        /// Kill a specified running process.
        /// Forces the specified Process to shutdown.  The kill message jumps 
        /// ahead of any messages already in the process's queue.  Any Process
        /// that has a persistent inbox or state will also have its persistent
        /// data wiped.  If the Process is registered it will also its 
        /// registration revoked.
        /// If you wish for the data to be maintained for future
        /// spawns then call Process.shutdown(pid);
        /// </summary>
        public static Aff<RT, Unit> kill(Eff<RT, ProcessId> pid) =>
            pid.Bind(kill);

        /// <summary>
        /// Send StartupProcess message to a process that isn't running (e.g. spawned with Lazy = true)
        /// </summary>
        public static Aff<RT, Unit> startup(ProcessId pid) =>
            Eff(() => Process.startup(pid));

        /// <summary>
        /// Send StartupProcess message to a process that isn't running (e.g. spawned with Lazy = true)
        /// </summary>
        public static Aff<RT, Unit> startup(Eff<RT, ProcessId> pid) =>
            pid.Bind(startup);

        /// <summary>
        /// Shutdown a specified running process.
        /// Forces the specified Process to shutdown.  The shutdown message jumps 
        /// ahead of any messages already in the process's queue.  Any Process
        /// that has a persistent inbox or state will have its state maintained
        /// for future spawns.  If you wish for the data to be dropped then call
        /// Process.kill(pid)
        /// </summary>
        public static Aff<RT, Unit> shutdown(ProcessId pid) =>
            Eff(() => Process.shutdown(pid));

        /// <summary>
        /// Shutdown a specified running process.
        /// Forces the specified Process to shutdown.  The shutdown message jumps 
        /// ahead of any messages already in the process's queue.  Any Process
        /// that has a persistent inbox or state will have its state maintained
        /// for future spawns.  If you wish for the data to be dropped then call
        /// Process.kill(pid)
        /// </summary>
        public static Aff<RT, Unit> shutdown(Eff<RT, ProcessId> pid) =>
            pid.Bind(shutdown);

        /// <summary>
        /// Shutdown all processes on the specified process-system
        /// </summary>
        public static Aff<RT, Unit> shutdownSystem(SystemName system) =>
            Eff(() => Process.shutdownSystem(system));

        /// <summary>
        /// Shutdown all processes on all process-systems
        /// </summary>
        public static Aff<RT, Unit> shutdownAll =>
            Eff(Process.shutdownAll);

        /// <summary>
        /// Forces a running process to restart.  This will reset its state and drop
        /// any subscribers, or any of its subscriptions.
        /// </summary>
        public static Aff<RT, Unit> restart(ProcessId pid) =>
            Eff(() => Process.restart(pid));

        /// <summary>
        /// Forces a running process to restart.  This will reset its state and drop
        /// any subscribers, or any of its subscriptions.
        /// </summary>
        public static Aff<RT, Unit> restart(Eff<RT, ProcessId> pid) =>
            pid.Bind(restart);

        /// <summary>
        /// Pauses a running process.  Messages will still be accepted into the Process'
        /// inbox (unless the inbox is full); but they won't be processed until the
        /// Process is unpaused: <see cref="unpause(ProcessId)"/>
        /// </summary>
        /// <param name="pid">Process to pause</param>
        public static Aff<RT, Unit> pause(ProcessId pid) =>
            Eff(() => Process.pause(pid));

        /// <summary>
        /// Pauses a running process.  Messages will still be accepted into the Process'
        /// inbox (unless the inbox is full); but they won't be processed until the
        /// Process is unpaused: <see cref="unpause(ProcessId)"/>
        /// </summary>
        /// <param name="pid">Process to pause</param>
        public static Aff<RT, Unit> pause(Eff<RT, ProcessId> pid) =>
            pid.Bind(pause);

        /// <summary>
        /// Pauses a running process.  Messages will still be accepted into the Process'
        /// inbox (unless the inbox is full); but they won't be processed until the
        /// Process is unpaused: <see cref="unpause(ProcessId)"/> manually, or until the 
        /// delay expires.
        /// </summary>
        /// <param name="pid">Process to pause</param>
        public static Aff<RT, IDisposable> pauseFor(ProcessId pid, Time delay) =>
            Eff(() => Process.pauseFor(pid, delay));

        /// <summary>
        /// Pauses a running process.  Messages will still be accepted into the Process'
        /// inbox (unless the inbox is full); but they won't be processed until the
        /// Process is unpaused: <see cref="unpause(ProcessId)"/> manually, or until the 
        /// delay expires.
        /// </summary>
        /// <param name="pid">Process to pause</param>
        public static Aff<RT, IDisposable> pauseFor(Eff<RT, ProcessId> pid, Time delay) =>
            pid.Bind(p => pauseFor(p, delay));

        /// <summary>
        /// Un-pauses a paused process.  Messages that have built-up in the inbox whilst
        /// the Process was paused will be Processed immediately.
        /// </summary>
        /// <param name="pid">Process to un-pause</param>
        public static Aff<RT, Unit> unpause(ProcessId pid) =>
            Eff(() => Process.unpause(pid));

        /// <summary>
        /// Un-pauses a paused process.  Messages that have built-up in the inbox whilst
        /// the Process was paused will be Processed immediately.
        /// </summary>
        /// <param name="pid">Process to un-pause</param>
        public static Aff<RT, Unit> unpause(Eff<RT, ProcessId> pid) =>
            pid.Bind(unpause);

        /// <summary>
        /// Find out if a process exists
        /// 
        ///     Rules:
        ///         * Local processes   - the process must actually be alive and in-memory
        ///         * Remote processes  - the process must have an inbox to receive messages 
        ///                               and may be active, but it's not required.
        ///         * Dispatchers/roles - at least one process in the collection must exist(pid)
        ///         * JS processes      - not current supported
        /// </summary>
        /// <param name="pid">Process ID to check</param>
        /// <returns>True if exists</returns>
        public static Aff<RT, bool> exists(ProcessId pid) =>
            Eff(() => Process.exists(pid));

        /// <summary>
        /// Find out if a process exists
        /// 
        ///     Rules:
        ///         * Local processes   - the process must actually be alive and in-memory
        ///         * Remote processes  - the process must have an inbox to receive messages 
        ///                               and may be active, but it's not required.
        ///         * Dispatchers/roles - at least one process in the collection must exist(pid)
        ///         * JS processes      - not current supported
        /// </summary>
        /// <param name="pid">Process ID to check</param>
        /// <returns>True if exists</returns>
        public static Aff<RT, bool> exists(Eff<RT, ProcessId> pid) =>
            pid.Bind(exists);

        /// <summary>
        /// Find out if a process exists and is alive
        /// 
        ///     Rules:
        ///         * Local processes   - the process must actually be running
        ///         * Remote processes  - the process must actually be running
        ///         * Dispatchers/roles - at least one process in the collection must be running
        ///         * JS processes      - not current supported
        /// </summary>
        /// <param name="pid">Process ID to check</param>
        /// <returns>True if exists</returns>
        public static Aff<RT, bool> ping(ProcessId pid) =>
            Eff(() => Process.ping(pid));

        /// <summary>
        /// Find out if a process exists and is alive
        /// 
        ///     Rules:
        ///         * Local processes   - the process must actually be running
        ///         * Remote processes  - the process must actually be running
        ///         * Dispatchers/roles - at least one process in the collection must be running
        ///         * JS processes      - not current supported
        /// </summary>
        /// <param name="pid">Process ID to check</param>
        /// <returns>True if exists</returns>
        public static Aff<RT, bool> ping(Eff<RT, ProcessId> pid) =>
            pid.Bind(ping);

        /// <summary>
        /// Watch another Process in case it terminates
        /// </summary>
        /// <param name="pid">Process to watch</param>
        public static Aff<RT, Unit> watch(ProcessId pid) =>
            Eff(() => Process.watch(pid));

        /// <summary>
        /// Watch another Process in case it terminates
        /// </summary>
        /// <param name="pid">Process to watch</param>
        public static Aff<RT, Unit> watch(Eff<RT, ProcessId> pid) =>
            pid.Bind(watch);

        /// <summary>
        /// Un-watch another Process that this Process has been watching
        /// </summary>
        /// <param name="pid">Process to watch</param>
        public static Aff<RT, Unit> unwatch(ProcessId pid) =>
            Eff(() => Process.unwatch(pid));

        /// <summary>
        /// Un-watch another Process that this Process has been watching
        /// </summary>
        /// <param name="pid">Process to watch</param>
        public static Aff<RT, Unit> unwatch(Eff<RT, ProcessId> pid) =>
            pid.Bind(unwatch);

        /// <summary>
        /// Watch for the death of the watching process and tell the watcher
        /// process when that happens.
        /// </summary>
        /// <param name="watcher">Watcher</param>
        /// <param name="watching">Watched</param>
        public static Aff<RT, Unit> watch(ProcessId watcher, ProcessId watching) =>
            Eff(() => Process.watch(watcher, watching));

        /// <summary>
        /// Watch for the death of the watching process and tell the watcher
        /// process when that happens.
        /// </summary>
        /// <param name="watcher">Watcher</param>
        /// <param name="watching">Watched</param>
        public static Aff<RT, Unit> watch(Eff<RT, ProcessId> watcher, Eff<RT, ProcessId> watching) =>
            from w1 in watcher
            from w2 in watching
            from rt in watch(w1, w2)
            select unit;

        /// <summary>
        /// Stop watching for the death of the watching process
        /// </summary>
        /// <param name="watcher">Watcher</param>
        /// <param name="watching">Watched</param>
        public static Aff<RT, Unit> unwatch(ProcessId watcher, ProcessId watching) =>
            Eff(() => Process.unwatch(watcher, watching));

        /// <summary>
        /// Stop watching for the death of the watching process
        /// </summary>
        /// <param name="watcher">Watcher</param>
        /// <param name="watching">Watched</param>
        public static Aff<RT, Unit> unwatch(Eff<RT, ProcessId> watcher, Eff<RT, ProcessId> watching) =>
            from w1 in watcher
            from w2 in watching
            from rt in unwatch(w1, w2)
            select unit;

        /// <summary>
        /// Find the number of items in the Process inbox
        /// </summary>
        /// <param name="pid">Process</param>
        /// <returns>Number of items in the Process inbox</returns>
        public static Aff<RT, int> inboxCount(ProcessId pid) =>
            Eff(() => Process.inboxCount(pid));

        /// <summary>
        /// Find the number of items in the Process inbox
        /// </summary>
        /// <param name="pid">Process</param>
        /// <returns>Number of items in the Process inbox</returns>
        public static Aff<RT, int> inboxCount(Eff<RT, ProcessId> pid) =>
            pid.Bind(inboxCount);

        /// <summary>
        /// Return True if the message sent is a Tell and not an Ask
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Eff<RT, bool> isTell =>
            echoState.Map(es => es.InMessageLoop 
                                    ? es.Request?.CurrentRequest == null
                                    : Process.raiseUseInMsgLoopOnlyException<bool>(nameof(isTell)));

        /// <summary>
        /// Get a list of cluster nodes that are online
        /// </summary>
        public static Eff<RT, HashMap<ProcessName, ClusterNode>> ClusterNodes =>
            echoState.Map(es => es.System.ClusterState?.Members.ToHashMap() ?? HashMap<ProcessName, ClusterNode>());

        /// <summary>
        /// List of system names running on this node
        /// </summary>
        public static Eff<RT, Seq<SystemName>> Systems =>
            echoState.Map(es => es.SystemNames.ToSeq());

        /// <summary>
        /// Contextual system name
        /// </summary>
        public static Eff<RT, SystemName> CurrentSystem =>
            echoState.Map(es => es.System.SystemName);

        /// <summary>
        /// Return True if the message sent is an Ask and not a Tell
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Eff<RT, bool> isAsk =>
            isTell.Map(not);

        /// <summary>
        /// Resolves a ProcessId into the absolute ProcessIds that it represents
        /// This allows live resolution of role-based ProcessIds to their real node
        /// ProcessIds.  
        /// </summary>
        /// <remarks>Mostly useful for debugging, but could be useful for layering
        /// additional logic to any message dispatch.
        /// </remarks>
        /// <param name="pid"></param>
        /// <returns>Enumerable of resolved ProcessIds - could be zero length</returns>
        public static Eff<RT, IEnumerable<ProcessId>> resolve(ProcessId pid) =>
            Eff(() => ActorContext.System(pid).ResolveProcessIdSelection(pid));

        /// <summary>
        /// Resolves a ProcessId into the absolute ProcessIds that it represents
        /// This allows live resolution of role-based ProcessIds to their real node
        /// ProcessIds.  
        /// </summary>
        /// <remarks>Mostly useful for debugging, but could be useful for layering
        /// additional logic to any message dispatch.
        /// </remarks>
        /// <param name="pid"></param>
        /// <returns>Enumerable of resolved ProcessIds - could be zero length</returns>
        public static Eff<RT, IEnumerable<ProcessId>> resolve(Eff<RT, ProcessId> pid) =>
            pid.Bind(resolve);

        /// <summary>
        /// Get the types of messages that the provided ProcessId accepts.  Returns
        /// an empty list if it can't be resolved for whatever reason (process doesn't
        /// exist/JS process/etc.).
        /// </summary>
        /// <param name="pid">Process ID to query</param>
        /// <returns>List of types</returns>
        public static Aff<RT, IEnumerable<Type>> validMessageTypes(ProcessId pid) =>
            Eff(() => ActorContext.System(pid).GetDispatcher(pid).GetValidMessageTypes());

        /// <summary>
        /// Get the types of messages that the provided ProcessId accepts.  Returns
        /// an empty list if it can't be resolved for whatever reason (process doesn't
        /// exist/JS process/etc.).
        /// </summary>
        /// <param name="pid">Process ID to query</param>
        /// <returns>List of types</returns>
        public static Aff<RT, IEnumerable<Type>> validMessageTypes(Eff<RT, ProcessId> pid) =>
            pid.Bind(validMessageTypes);

        /// <summary>
        /// Cancel an already scheduled message
        /// </summary>
        public static Aff<RT, Unit> cancelScheduled(ProcessId pid, string key) =>
            Eff(() => Process.cancelScheduled(pid, key));

        /// <summary>
        /// Cancel an already scheduled message
        /// </summary>
        public static Aff<RT, Unit> cancelScheduled(Eff<RT, ProcessId> pid, string key) =>
            pid.Bind(p => cancelScheduled(p, key));
    }
}
