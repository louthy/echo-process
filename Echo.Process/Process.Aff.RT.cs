using System;
using LanguageExt;
using static LanguageExt.Prelude;
using System.Collections.Generic;
using System.Linq;
using LanguageExt.UnitsOfMeasure;
using LanguageExt.Common;
using LanguageExt.Pipes;

namespace Echo;

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
    where RT : struct, HasEcho<RT>
{
    static Process()
    {
        System = Eff<RT, SystemName>(_ => ActorContext.Self.System);
        Self = Eff<RT, ProcessId>(_ => Process.Self);
        Parent = Eff<RT, ProcessId>(_ => Process.Parent);
        Sender = Eff<RT, ProcessId>(_ => Process.Sender);
        User = Eff<RT, ProcessId>(_ => Process.User());
        DeadLetters = System.Map(Process.DeadLetters);
        Root = System.Map(Process.Root);
        Errors = System.Map(Process.Errors);
        Children = Eff<RT, HashMap<string, ProcessId>>(_ => Process.Children);
        ChildrenInOrder = Children.Map(hm => toMap(hm));
        Request = Eff<RT, ActorRequestContext>(_ => ActorContext.Request);
        killSelf = Request.Bind(_ => FailEff<Unit>(new ProcessKillException()));
        shutdownSelf = Self.Bind(shutdown);
    }
    
    /// <summary>
    /// Internal use: Access to the request that caused the current inbox process
    /// </summary>
    static readonly Eff<RT, ActorRequestContext> Request;
    
    /// <summary>
    /// Current system
    /// </summary>
    /// <remarks>
    /// TODO: This is a placeholder until the current-system becomes part of the HasEcho trait.  
    /// Its current behaviour is to return the system of the process being run, or if not inside an inbox,
    /// the default-system. 
    /// </remarks>
    public static readonly Eff<RT, SystemName> System;

    /// <summary>
    /// Current process ID
    /// </summary>
    /// <remarks>
    /// This should be used from within a process message loop only
    /// </remarks>
    public static readonly Eff<RT, ProcessId> Self;

    /// <summary>
    /// Parent process ID
    /// </summary>
    /// <remarks>
    /// This should be used from within a process message loop only
    /// </remarks>
    public static readonly Eff<RT, ProcessId> Parent;

    /// <summary>
    /// Sender process ID
    /// Always valid even if there's not a sender (the 'NoSender' process ID will
    /// be provided).
    /// </summary>
    public static readonly Eff<RT, ProcessId> Sender;

    /// <summary>
    /// User process ID
    /// The User process is the default entry process, your first process spawned
    /// will be a child of this process.
    /// </summary>
    public static readonly Eff<RT, ProcessId> User;

    /// <summary>
    /// Dead letters process
    /// Subscribe to it to monitor the failed messages (<see cref="subscribe(ProcessId)"/>)
    /// </summary>
    public static readonly Eff<RT, ProcessId> DeadLetters;

    /// <summary>
    /// Root process ID
    /// The Root process is the parent of all processes
    /// </summary>
    public static readonly Eff<RT, ProcessId> Root;

    /// <summary>
    /// Errors process
    /// Subscribe to it to monitor the errors thrown 
    /// </summary>
    public static readonly Eff<RT, ProcessId> Errors;

    /// <summary>
    /// Get the child processes of the running process
    /// </summary>
    /// <remarks>
    /// This should be used from within a process message loop only
    /// </remarks>
    public static readonly Eff<RT, HashMap<string, ProcessId>> Children;

    /// <summary>
    /// Get the child processes of the running process
    /// </summary>
    /// <remarks>
    /// This should be used from within a process message loop only
    /// </remarks>
    public static readonly Eff<RT, Map<string, ProcessId>> ChildrenInOrder;

    /// <summary>
    /// Get the child processes of the process ID provided
    /// </summary>
    public static Aff<RT, HashMap<string, ProcessId>> children(ProcessId pid) =>
        Eff<RT, HashMap<string, ProcessId>>(_ => Process.children(pid));

    /// <summary>
    /// Get the child processes of the process ID provided
    /// </summary>
    public static Aff<RT, Map<string, ProcessId>> childrenInOrder(ProcessId pid) =>
        children(pid).Map(hm => toMap(hm));

    /// <summary>
    /// Get the child processes by name
    /// </summary>
    public static Eff<RT, ProcessId> child(ProcessName name) =>
        Self.Map(slf => slf[name]);
    
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
        from req in Request
        from chs in SuccessEff(req.Children) 
        from res in chs.IsEmpty
                        ? FailEff<ProcessId>(new NoChildProcessesException())
                        : SuccessEff(chs.Values.Skip(index % chs.Count).Head())
        select res;

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
    public static readonly Eff<RT, Unit> killSelf;

    /// <summary>
    /// Shutdown the currently running process.  The shutdown message jumps 
    /// ahead of any messages already in the process's queue but doesn't exit
    /// immediately like kill().  Any Process that has a persistent inbox or 
    /// state will have its state maintained for future spawns.  If you wish 
    /// for the data to be dropped then call Process.kill()
    /// </summary>
    public static readonly Eff<RT, Unit> shutdownSelf;

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
    public static Eff<RT, Unit> kill(ProcessId pid) =>
        Eff<RT, Unit>(_ => Process.kill(pid));

    /// <summary>
    /// Send StartupProcess message to a process that isn't running (e.g. spawned with Lazy = true)
    /// </summary>
    public static Eff<RT, Unit> startup(ProcessId pid) =>
        Eff<RT, Unit>(_ => Process.startup(pid));

    /// <summary>
    /// Shutdown a specified running process.
    /// Forces the specified Process to shutdown.  The shutdown message jumps 
    /// ahead of any messages already in the process's queue.  Any Process
    /// that has a persistent inbox or state will have its state maintained
    /// for future spawns.  If you wish for the data to be dropped then call
    /// Process.kill(pid)
    /// </summary>
    public static Eff<RT, Unit> shutdown(ProcessId pid) =>
        Eff<RT, Unit>(_ => Process.shutdown(pid));

    /// <summary>
    /// Shutdown all processes on the specified process-system
    /// </summary>
    public static Eff<RT, Unit> shutdownSystem(SystemName system) =>
        Eff<RT, Unit>(_ => Process.shutdownSystem(system));

    /// <summary>
    /// Shutdown all processes on all process-systems
    /// </summary>
    public static Eff<RT, Unit> shutdownAll =>
        Eff<RT, Unit>(_ => Process.shutdownAll());

    /// <summary>
    /// Forces a running process to restart.  This will reset its state and drop
    /// any subscribers, or any of its subscriptions.
    /// </summary>
    public static Eff<RT, Unit> restart(ProcessId pid) =>
        Eff<RT, Unit>(_ => Process.restart(pid));

    /// <summary>
    /// Pauses a running process.  Messages will still be accepted into the Process'
    /// inbox (unless the inbox is full); but they won't be processed until the
    /// Process is un-paused: <see cref="unpause(ProcessId)"/>
    /// </summary>
    /// <param name="pid">Process to pause</param>
    public static Eff<RT, Unit> pause(ProcessId pid) =>
        Eff<RT, Unit>(_ => Process.pause(pid));

    /// <summary>
    /// Pauses a running process.  Messages will still be accepted into the Process'
    /// inbox (unless the inbox is full); but they won't be processed until the
    /// Process is unpaused: <see cref="unpause(ProcessId)"/> manually, or until the 
    /// delay expires.
    /// </summary>
    /// <param name="pid">Process to pause</param>
    /// <param name="delay">How long to pause for</param>
    public static Eff<RT, IDisposable> pauseFor(ProcessId pid, Time delay) =>
        Eff<RT, IDisposable>(_ => Process.pauseFor(pid, delay));

    /// <summary>
    /// Un-pauses a paused process.  Messages that have built-up in the inbox whilst
    /// the Process was paused will be Processed immediately.
    /// </summary>
    /// <param name="pid">Process to un-pause</param>
    public static Eff<RT, Unit> unpause(ProcessId pid) =>
        Eff<RT, Unit>(_ => Process.unpause(pid));

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
        Eff<RT, bool>(_ => Process.exists(pid));

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
        Eff<RT, bool>(_ => Process.ping(pid));

    /// <summary>
    /// Watch another Process in case it terminates
    /// </summary>
    /// <param name="pid">Process to watch</param>
    public static Eff<RT, Unit> watch(ProcessId pid) =>
        Eff<RT, Unit>(_ => Process.watch(pid));

    /// <summary>
    /// Un-watch another Process that this Process has been watching
    /// </summary>
    /// <param name="pid">Process to watch</param>
    public static Aff<RT, Unit> unwatch(ProcessId pid) =>
        Eff<RT, Unit>(_ => Process.unwatch(pid));

    /// <summary>
    /// Watch for the death of the watching process and tell the watcher
    /// process when that happens.
    /// </summary>
    /// <param name="watcher">Watcher</param>
    /// <param name="watching">Watched</param>
    public static Aff<RT, Unit> watch(ProcessId watcher, ProcessId watching) =>
        Eff<RT, Unit>(_ => Process.watch(watcher, watching));

    /// <summary>
    /// Stop watching for the death of the watching process
    /// </summary>
    /// <param name="watcher">Watcher</param>
    /// <param name="watching">Watched</param>
    public static Aff<RT, Unit> unwatch(ProcessId watcher, ProcessId watching) =>
        Eff<RT, Unit>(_ => Process.unwatch(watcher, watching));

    /// <summary>
    /// Find the number of items in the Process inbox
    /// </summary>
    /// <param name="pid">Process</param>
    /// <returns>Number of items in the Process inbox</returns>
    public static Aff<RT, int> inboxCount(ProcessId pid) =>
        Eff<RT, int>(_ => Process.inboxCount(pid));

    /// <summary>
    /// Return True if the message sent is a Tell and not an Ask
    /// </summary>
    /// <remarks>
    /// This should be used from within a process' message loop only
    /// </remarks>
    public static Eff<RT, bool> isTell =>
        Eff<RT, bool>(_ => Process.isTell);

    /// <summary>
    /// Assert that the current request is a tell
    /// </summary>
    /// <remarks>
    /// Fails with 'Tell expected' if the current request isn't a tell
    /// Fails with 'Not in a message loop' if we're not in a Process
    /// </remarks>
    public static Eff<RT, Unit> assertTell =>
        isTell.Bind(x => x ? unitEff : FailEff<Unit>(Error.New("Tell expected"))); 

    /// <summary>
    /// Get a list of cluster nodes that are online
    /// </summary>
    public static Eff<RT, HashMap<ProcessName, ClusterNode>> ClusterNodes =>
        System.Map(Process.ClusterNodes);

    /// <summary>
    /// List of system names running on this node
    /// </summary>
    public static Eff<RT, Seq<SystemName>> Systems =>
        Eff<RT, Seq<SystemName>>(_ => ActorContext.Systems.ToSeq());

    /// <summary>
    /// Return True if the message sent is an Ask and not a Tell
    /// </summary>
    /// <remarks>
    /// This should be used from within a process' message loop only
    /// </remarks>
    public static Eff<RT, bool> isAsk => 
        isTell.Map(not);

    /// <summary>
    /// Assert that the current request is an ask
    /// </summary>
    /// <remarks>
    /// Fails with 'Ask expected' if the current request isn't a ask
    /// Fails with 'Not in a message loop' if we're not in a Process
    /// </remarks>
    public static Eff<RT, Unit> assertAsk =>
        isAsk.Bind(x => x ? unitEff : FailEff<Unit>(Error.New("Ask expected"))); 

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
        Eff<RT, IEnumerable<ProcessId>>(_ => Process.resolve(pid));

    /// <summary>
    /// Get the types of messages that the provided ProcessId accepts.  Returns
    /// an empty list if it can't be resolved for whatever reason (process doesn't
    /// exist/JS process/etc.).
    /// </summary>
    /// <param name="pid">Process ID to query</param>
    /// <returns>List of types</returns>
    public static Aff<RT, IEnumerable<Type>> validMessageTypes(ProcessId pid) =>
        Eff<RT, IEnumerable<Type>>(_ => Process.validMessageTypes(pid));

    /// <summary>
    /// Re-schedule an already scheduled message
    /// </summary>
    public static Eff<RT, Unit> reschedule(ProcessId pid, string key, DateTime when) =>
        Eff<RT, Unit>(_ => Process.reschedule(pid, key, when));

    /// <summary>
    /// Re-schedule an already scheduled message
    /// </summary>
    public static Eff<RT, Unit> reschedule(ProcessId pid, string key, TimeSpan when) =>
        Eff<RT, Unit>(_ => Process.reschedule(pid, key, when));

    /// <summary>
    /// Cancel an already scheduled message
    /// </summary>
    public static Eff<RT, Unit> cancelScheduled(ProcessId pid, string key) =>
        Eff<RT, Unit>(_ => Process.cancelScheduled(pid, key));
}
