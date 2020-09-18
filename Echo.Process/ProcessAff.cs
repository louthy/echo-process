using System;
using System.Linq;
using LanguageExt;
using System.Threading;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Reactive.Subjects;
using static LanguageExt.Prelude;
using System.Collections.Generic;
using LanguageExt.UnitsOfMeasure;
using System.Reactive.Concurrency;
using Echo.Config;
using LanguageExt.Common;

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
    public static partial class ProcessAff<RT>
        where RT : struct, HasEcho<RT>
    {
        /// <summary>
        /// Run the `ma` operation in the context of the named system. 
        /// </summary>
        /// <remarks>
        /// This is useful when running multiple systems and you want to use a system other than the default.  If you're
        /// only running one system, or you want to use the default-system then this isn't needed.
        /// </remarks>
        /// <param name="name">Name of system to contextualise</param>
        /// <param name="ma">Operation to run within the context</param>
        /// <returns>Result of ma</returns>
        public static Eff<RT, A> withSys<A>(SystemName name, Eff<RT, A> ma) =>
            ActorContextAff<RT>.localSystem(name, ma);
        
        /// <summary>
        /// Run the `ma` operation in the context of the named system. 
        /// </summary>
        /// <remarks>
        /// This is useful when running multiple systems and you want to use a system other than the default.  If you're
        /// only running one system, or you want to use the default-system then this isn't needed.
        /// </remarks>
        /// <param name="name">Name of system to contextualise</param>
        /// <param name="ma">Operation to run within the context</param>
        /// <returns>Result of ma</returns>
        public static Aff<RT, A> withSys<A>(SystemName name, Aff<RT, A> ma) =>
            ActorContextAff<RT>.localSystem(name, ma);
        
        /// <summary>
        /// Current process ID
        /// </summary>
        /// <remarks>
        /// This should be used from within a process message loop only
        /// </remarks>
        public static Eff<RT, ProcessId> Self =>
            ActorContextAff<RT>.Self;

        /// <summary>
        /// Parent process ID
        /// </summary>
        /// <remarks>
        /// This should be used from within a process message loop only
        /// </remarks>
        public static Eff<RT, ProcessId> Parent =>
            ActorContextAff<RT>.Parent;

        /// <summary>
        /// Sender process ID
        /// Always valid even if there's not a sender (the 'NoSender' process ID will
        /// be provided).
        /// </summary>
        public static Eff<RT, ProcessId> Sender =>
            ActorContextAff<RT>.Request.Map(r => r.Sender) | User;

        /// <summary>
        /// User process ID
        /// The User process is the default entry process, your first process spawned
        /// will be a child of this process.
        /// </summary>
        public static Eff<RT, ProcessId> User =>
            ActorContextAff<RT>.LocalSystem.Map(sys => sys.User);

        /// <summary>
        /// Dead letters process
        /// Subscribe to it to monitor the failed messages (<see cref="subscribe(ProcessId)"/>)
        /// </summary>
        public static Eff<RT, ProcessId> DeadLetters =>
            ActorContextAff<RT>.LocalSystem.Map(sys => sys.DeadLetters);

        /// <summary>
        /// Root process ID
        /// The Root process is the parent of all processes
        /// </summary>
        public static Eff<RT, ProcessId> Root =>
            ActorContextAff<RT>.LocalSystem.Map(sys => sys.Root);

        /// <summary>
        /// Errors process
        /// Subscribe to it to monitor the errors thrown 
        /// </summary>
        public static Eff<RT, ProcessId> Errors =>
            ActorContextAff<RT>.LocalSystem.Map(sys => sys.Errors);

        /// <summary>
        /// Get the child processes of the running process
        /// </summary>
        /// <remarks>
        /// This should be used from within a process message loop only
        /// </remarks>
        public static Eff<RT, HashMap<string, ProcessId>> Children =>
            ActorContextAff<RT>.Request.Map(r => r.Children);

        /// <summary>
        /// Get the child processes of the running process
        /// </summary>
        /// <remarks>
        /// This should be used from within a process message loop only
        /// </remarks>
        public static Eff<RT, Map<string, ProcessId>> ChildrenInOrder =>
            Children.Map(hm => toMap(hm));

        /// <summary>
        /// Get the child processes of the process ID provided
        /// </summary>
        public static Aff<RT, HashMap<string, ProcessId>> children(ProcessId pid) =>
            ActorSystemAff<RT>.getChildren(pid);

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
            from req in ActorContextAff<RT>.Request
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
        public static Eff<RT, Unit> kill() =>
            from req in ActorContextAff<RT>.Request
            from res in FailEff<Unit>(new ProcessKillException())
            select res;

        /// <summary>
        /// Shutdown the currently running process.  The shutdown message jumps 
        /// ahead of any messages already in the process's queue but doesn't exit
        /// immediately like kill().  Any Process that has a persistent inbox or 
        /// state will have its state maintained for future spawns.  If you wish 
        /// for the data to be dropped then call Process.kill()
        /// </summary>
        public static Aff<RT, Unit> shutdown() =>
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
            ActorSystemAff<RT>.kill(pid, false);

        /// <summary>
        /// Send StartupProcess message to a process that isn't running (e.g. spawned with Lazy = true)
        /// </summary>
        public static Aff<RT, Unit> startup(ProcessId pid) =>
            ActorSystemAff<RT>.tellSystem(pid, SystemMessage.StartupProcess(false));

        /// <summary>
        /// Shutdown a specified running process.
        /// Forces the specified Process to shutdown.  The shutdown message jumps 
        /// ahead of any messages already in the process's queue.  Any Process
        /// that has a persistent inbox or state will have its state maintained
        /// for future spawns.  If you wish for the data to be dropped then call
        /// Process.kill(pid)
        /// </summary>
        public static Aff<RT, Unit> shutdown(ProcessId pid) =>
            ActorSystemAff<RT>.kill(pid, true);

        /// <summary>
        /// Shutdown all processes on the specified process-system
        /// </summary>
        public static Aff<RT, Unit> shutdownSystem(SystemName system) =>
            ActorContextAff<RT>.stopSystem(system);

        /// <summary>
        /// Shutdown all processes on all process-systems
        /// </summary>
        public static Aff<RT, Unit> shutdownAll =>
            from _ in Eff(ProcessConfig.ShutdownLocalScheduler)
            from r in ActorContextAff<RT>.stopAllSystems
            select r;

        /// <summary>
        /// Forces a running process to restart.  This will reset its state and drop
        /// any subscribers, or any of its subscriptions.
        /// </summary>
        public static Aff<RT, Unit> restart(ProcessId pid) =>
            ActorSystemAff<RT>.tellSystem(pid, SystemMessage.Restart);

        /// <summary>
        /// Pauses a running process.  Messages will still be accepted into the Process'
        /// inbox (unless the inbox is full); but they won't be processed until the
        /// Process is un-paused: <see cref="unpause(ProcessId)"/>
        /// </summary>
        /// <param name="pid">Process to pause</param>
        public static Aff<RT, Unit> pause(ProcessId pid) =>
            ActorSystemAff<RT>.tellSystem(pid, SystemMessage.Pause);

        /// <summary>
        /// Pauses a running process.  Messages will still be accepted into the Process'
        /// inbox (unless the inbox is full); but they won't be processed until the
        /// Process is unpaused: <see cref="unpause(ProcessId)"/> manually, or until the 
        /// delay expires.
        /// </summary>
        /// <param name="pid">Process to pause</param>
        /// <param name="delay">How long to pause for</param>
        public static Eff<RT, Unit> pauseFor(ProcessId pid, Time delay) =>
            Eff<RT, Unit>(env => {

                var op = from _ in pause(pid)
                         from d in IO.Time.sleepFor<RT>(delay)
                         from r in unpause(pid)
                         select r;
                  
                // We don't await
                op.RunIO(env);
                return unit;
            });

        /// <summary>
        /// Un-pauses a paused process.  Messages that have built-up in the inbox whilst
        /// the Process was paused will be Processed immediately.
        /// </summary>
        /// <param name="pid">Process to un-pause</param>
        public static Aff<RT, Unit> unpause(ProcessId pid) =>
            ActorSystemAff<RT>.tellSystem(pid, SystemMessage.Unpause);

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
            ActorSystemAff<RT>.getDispatcher(pid).Bind(d => d.Exists<RT>());

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
            ActorSystemAff<RT>.getDispatcher(pid).Bind(d => d.Ping<RT>());

        /// <summary>
        /// Watch another Process in case it terminates
        /// </summary>
        /// <param name="pid">Process to watch</param>
        public static Aff<RT, Unit> watch(ProcessId pid) =>
            from req in ActorContextAff<RT>.Request
            from res in ActorSystemAff<RT>.getDispatcher(req.Self.Actor.Id).Bind(d => d.DispatchWatch<RT>(pid))
            select res;

        /// <summary>
        /// Un-watch another Process that this Process has been watching
        /// </summary>
        /// <param name="pid">Process to watch</param>
        public static Aff<RT, Unit> unwatch(ProcessId pid) =>
            from req in ActorContextAff<RT>.Request
            from res in ActorSystemAff<RT>.getDispatcher(req.Self.Actor.Id).Bind(d => d.DispatchUnWatch<RT>(pid))
            select res;

        /// <summary>
        /// Watch for the death of the watching process and tell the watcher
        /// process when that happens.
        /// </summary>
        /// <param name="watcher">Watcher</param>
        /// <param name="watching">Watched</param>
        public static Aff<RT, Unit> watch(ProcessId watcher, ProcessId watching) =>
            ActorSystemAff<RT>.getDispatcher(watcher).Bind(d => d.DispatchWatch<RT>(watching));

        /// <summary>
        /// Stop watching for the death of the watching process
        /// </summary>
        /// <param name="watcher">Watcher</param>
        /// <param name="watching">Watched</param>
        public static Aff<RT, Unit> unwatch(ProcessId watcher, ProcessId watching) =>
            ActorSystemAff<RT>.getDispatcher(watcher).Bind(d => d.DispatchUnWatch<RT>(watching));

        /// <summary>
        /// Find the number of items in the Process inbox
        /// </summary>
        /// <param name="pid">Process</param>
        /// <returns>Number of items in the Process inbox</returns>
        public static Aff<RT, int> inboxCount(ProcessId pid) =>
            ActorSystemAff<RT>.getDispatcher(pid).Bind(d => d.GetInboxCount<RT>());

        /// <summary>
        /// Return True if the message sent is a Tell and not an Ask
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Eff<RT, bool> isTell =>
            ActorContextAff<RT>.Request.Map(r => r.CurrentRequest == null);

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
            ActorContextAff<RT>.LocalSystem.Map(sys => sys.ClusterState.Value.Members);

        /// <summary>
        /// List of system names running on this node
        /// </summary>
        public static Eff<Seq<SystemName>> Systems =>
            ActorContext.SystemNames;

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
        public static IEnumerable<ProcessId> resolve(ProcessId pid) =>
            ActorSystemAff<RT>.resolveProcessIdSelection(pid);

        /// <summary>
        /// Get the types of messages that the provided ProcessId accepts.  Returns
        /// an empty list if it can't be resolved for whatever reason (process doesn't
        /// exist/JS process/etc.).
        /// </summary>
        /// <param name="pid">Process ID to query</param>
        /// <returns>List of types</returns>
        public static Aff<RT, IEnumerable<Type>> validMessageTypes(ProcessId pid) =>
            ActorSystemAff<RT>.getDispatcher(pid).Bind(s => s.GetValidMessageTypes<RT>());

        /// <summary>
        /// Re-schedule an already scheduled message
        /// </summary>
        public static Aff<RT, Unit> reschedule(ProcessId pid, string key, DateTime when) =>
            from inboxKey in SuccessEff(ActorInboxCommon.ClusterScheduleKey(pid))
            from _        in SuccessEff(LocalScheduler.Reschedule(pid, key, when))
            from r        in ProcessAff<RT>.tell(pid.Take(1).Child("system").Child("scheduler"), Scheduler.Msg.Reschedule(inboxKey, key, when))
            select r;

        /// <summary>
        /// Re-schedule an already scheduled message
        /// </summary>
        public static Aff<RT, Unit> reschedule(ProcessId pid, string key, TimeSpan when) =>
            reschedule(pid, key, DateTime.Now.Add(when));

        /// <summary>
        /// Cancel an already scheduled message
        /// </summary>
        public static Aff<RT, Unit> cancelScheduled(ProcessId pid, string key) =>
            from inboxKey in SuccessEff(ActorInboxCommon.ClusterScheduleKey(pid))
            from _        in SuccessEff(LocalScheduler.RemoveExistingScheduledMessage(pid, key))
            from r        in ProcessAff<RT>.tell(pid.Take(1).Child("system").Child("scheduler"), Scheduler.Msg.RemoveFromSchedule(inboxKey, key))
            select r;
    }
}