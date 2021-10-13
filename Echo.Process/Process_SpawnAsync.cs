using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static LanguageExt.Prelude;

namespace Echo
{
    /// <summary>
    /// <para>
    ///     Process: Spawn functions
    /// </para>
    /// <para>
    ///     The spawn functions create a new process.  Processes are either simple message receivers that
    ///     don't manage state and therefore only need a messageHandler.  Or they manage state over time.
    /// </para>
    /// <para>
    ///     If they manage state then you should also provide a 'setup' function that generates the initial
    ///     state.  This allows the process system to recover if a process crashes.  It can re-call the 
    ///     setup function to reset the state and continue processing the messages.
    /// </para>
    /// </summary>
    public static partial class Process
    {
        /// <summary>
        /// Create a new process by name.  
        /// If this is called from within a process' message loop 
        /// then the new process will be a child of the current process.  If it is called from
        /// outside of a process, then it will be made a child of the root 'user' process.
        /// </summary>
        /// <typeparam name="T">Type of messages that the child-process can accept</typeparam>
        /// <param name="Name">Name of the child-process</param>
        /// <param name="Inbox">Function that is the process</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <param name="Terminated">Message function to call when a Process [that this Process
        /// watches] terminates</param>
        /// <param name="Lazy">If set to true actor will not start automatically, you need to
        /// startup(processId) manually</param>
        /// <param name="MaxMailboxSize">Maximum number of messages to queue</param>
        /// <param name="Shutdown">Optional shutdown function</param>
        /// <param name="System">Echo process system to spawn in</param>
        /// <returns>A ProcessId that identifies the child</returns>
        public static ProcessId spawnAsync<T>(
            ProcessName Name,
            Func<T, ValueTask> Inbox,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            Func<ProcessId, ValueTask> Terminated = null,
            Func<ValueTask> Shutdown = null,
            SystemName System = default(SystemName),
            bool Lazy = false
            )
        {
            if (System.IsValid && ActorContext.Request != null) throw new ProcessException("When spawning you can only specify a System from outside of a Process", ActorContext.Self[Name].Path, "");

            var sys = System.IsValid
                ? ActorContext.System(System)
                : ActorContext.DefaultSystem;

            var parent = System.IsValid
                ? sys.UserContext.Self
                : ActorContext.SelfProcess;

            return sys.ActorCreateAsync(parent, Name, Inbox, Shutdown, Terminated, Strategy, Flags, MaxMailboxSize, Lazy);
        }        
        
        /// <summary>
        /// Create a new process by name.  
        /// If this is called from within a process' message loop 
        /// then the new process will be a child of the current process.  If it is called from
        /// outside of a process, then it will be made a child of the root 'user' process.
        /// </summary>
        /// <typeparam name="S">Type of state</typeparam>
        /// <typeparam name="T">Type of messages that the child-process can accept</typeparam>
        /// <param name="Name">Name of the child-process</param>
        /// <param name="Setup">Startup and restart function</param>
        /// <param name="Inbox">Function that is the process</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <param name="Terminated">Message function to call when a Process [that this Process
        /// watches] terminates</param>
        /// <param name="Lazy">If set to true actor will not start automatically, you need to
        /// startup(processId) manually</param>
        /// <param name="MaxMailboxSize">Maximum number of messages to queue</param>
        /// <param name="Shutdown">Optional shutdown function</param>
        /// <param name="System">Echo process system to spawn in</param>
        /// <returns>A ProcessId that identifies the child</returns>
        public static ProcessId spawnAsync<S, T>(
            ProcessName Name,
            Func<ValueTask<S>> Setup,
            Func<S, T, ValueTask<S>> Inbox,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            Func<S, ProcessId, ValueTask<S>> Terminated = null,
            Func<S, ValueTask<Unit>> Shutdown = null,
            SystemName System = default(SystemName),
            bool Lazy = false
            )
        {
            if (System.IsValid && ActorContext.Request != null) throw new ProcessException("When spawning you can only specify a System from outside of a Process", ActorContext.Self[Name].Path, "");

            var sys = System.IsValid
                ? ActorContext.System(System)
                : ActorContext.DefaultSystem;

            var parent = System.IsValid
                ? sys.UserContext.Self
                : ActorContext.SelfProcess;

            return sys.ActorCreateAsync(parent, Name, Inbox, Setup, Shutdown, Terminated, Strategy, Flags, MaxMailboxSize, Lazy);
        }

        /// <summary>
        /// Create N child processes.
        /// The name provided will be used as a basis to generate the child names.  Each child will
        /// be named "name-index" where index starts at zero.  
        /// If this is called from within a process' message loop 
        /// then the new processes will be a children of the current process.  If it is called from
        /// outside of a process, then they will be made a child of the root 'user' process.
        /// </summary>
        /// <typeparam name="S">Type of process's aggregate state</typeparam>
        /// <typeparam name="T">Type of messages that the child-process can accept</typeparam>
        /// <param name="Count">Number of processes to spawn</param>
        /// <param name="Setup">Startup and restart function</param>
        /// <param name="Name">Name of the child-process</param>
        /// <param name="Inbox">Function that is the process</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <param name="MaxMailboxSize">Maximum inbox size</param>
        /// <param name="Shutdown">Optional shutdown function</param>
        /// <param name="Terminated">Message function to call when a Process [that this Process
        /// watches] terminates</param>
        /// <returns>ProcessId IEnumerable</returns>
        public static IEnumerable<ProcessId> spawnManyAsync<S, T>(
            int Count, 
            ProcessName Name, 
            Func<ValueTask<S>> Setup, 
            Func<S, T, ValueTask<S>> Inbox, 
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            Func<S, ValueTask<Unit>> Shutdown = null,
            Func<S, ProcessId, ValueTask<S>> Terminated = null
            ) =>
            Range(0, Count).Map(n => ActorContext.System(default(SystemName))
                                                 .ActorCreateAsync(
                                                      ActorContext.SelfProcess, 
                                                      $"{Name}-{n}", 
                                                      Inbox, 
                                                      Setup, 
                                                      Shutdown, 
                                                      Terminated, 
                                                      Strategy, 
                                                      Flags, 
                                                      MaxMailboxSize, 
                                                      false)).ToList();

        /// <summary>
        /// Create N child processes.
        /// The name provided will be used as a basis to generate the child names.  Each child will
        /// be named "name-index" where index starts at zero.  
        /// If this is called from within a process' message loop 
        /// then the new processes will be a children of the current process.  If it is called from
        /// outside of a process, then they will be made a child of the root 'user' process.
        /// </summary>
        /// <typeparam name="S">State type</typeparam>
        /// <typeparam name="T">Type of messages that the child-process can accept</typeparam>
        /// <param name="Spec">Map of IDs and State for generating child workers</param>
        /// <param name="Name">Name of the child-process</param>
        /// <param name="Inbox">Function that is the process</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <param name="MaxMailboxSize">Maximum inbox size</param>
        /// <param name="Shutdown">Optional shutdown function</param>
        /// <param name="Terminated">Message function to call when a Process [that this Process
        /// watches] terminates</param>
        /// <returns>ProcessId IEnumerable</returns>
        public static IEnumerable<ProcessId> spawnManyAsync<S, T>(
            ProcessName Name,
            HashMap<int, Func<ValueTask<S>>> Spec, 
            Func<S, T, ValueTask<S>> Inbox, 
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            Func<S, ValueTask<Unit>> Shutdown = null,
            Func<S, ProcessId, ValueTask<S>> Terminated = null
            ) =>
            Spec.Map((id,state) => ActorContext.System(default(SystemName))
                                               .ActorCreateAsync(ActorContext.SelfProcess, 
                                                            $"{Name}-{id}", 
                                                            Inbox, 
                                                            state, 
                                                            Shutdown, 
                                                            Terminated, 
                                                            Strategy, 
                                                            Flags, 
                                                            MaxMailboxSize, 
                                                            false)).Values.ToList();
    }
}
