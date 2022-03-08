using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Echo.Traits;
using LanguageExt.Effects.Traits;
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
    public static partial class Process<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT> 
    {
        /// <summary>
        /// Create a new process by name.  
        /// If this is called from within a process' message loop 
        /// then the new process will be a child of the current process.  If it is called from
        /// outside of a process, then it will be made a child of the root 'user' process.
        /// </summary>
        /// <typeparam name="RT">Aff runtime</typeparam>
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
        public static Eff<RT, ProcessId> spawn<T>(ProcessName Name,
            Func<T, Aff<RT, Unit>> Inbox,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            Func<ProcessId, Aff<RT, Unit>> Terminated = null,
            Func<Aff<RT, Unit>> Shutdown = null,
            SystemName System = default(SystemName),
            bool Lazy = false) =>
            spawn<Unit, T>(
                Name,
                () => unitAff,
                (_, m) => Inbox(m).Map(static _ => unit),
                Flags,
                Strategy,
                MaxMailboxSize,
                (_, p) => (Terminated ??= (_ => unitAff))(p).Map(static _ => unit),
                _ => (Shutdown ??= (() => unitAff))().Map(static _ => unit),
                System,
                Lazy);

        /// <summary>
        /// Create a new process by name.  
        /// If this is called from within a process' message loop 
        /// then the new process will be a child of the current process.  If it is called from
        /// outside of a process, then it will be made a child of the root 'user' process.
        /// </summary>
        /// <typeparam name="RT">Aff runtime</typeparam>
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
        public static Eff<RT, ProcessId> spawn<S, T>(
            ProcessName Name,
            Func<Aff<RT, S>> Setup,
            Func<S, T, Aff<RT, S>> Inbox,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            Func<S, ProcessId, Aff<RT, S>> Terminated = null,
            Func<S, Aff<RT, Unit>> Shutdown = null,
            SystemName System = default(SystemName),
            bool Lazy = false)
        {
            Shutdown   ??= (s => unitAff);
            Terminated ??= ((s, p) => SuccessAff(s));
            
            Func<ValueTask<S>> setup(RT runtime) =>
                async () => (await Setup().Run(runtime).ConfigureAwait(false)).ThrowIfFail();

            Func<S, T, ValueTask<S>> inbox(RT runtime) =>
                async (s, m) => (await Inbox(s, m).Run(runtime).ConfigureAwait(false)).ThrowIfFail();

            Func<S, ValueTask<Unit>> shut(RT runtime) =>
                async s => await Shutdown(s).RunUnit(runtime).ConfigureAwait(false);

            Func<S, ProcessId, ValueTask<S>> term(RT runtime) =>
                async (s, p) => (await Terminated(s, p).Run(runtime).ConfigureAwait(false)).ThrowIfFail();

            return Eff<RT, ProcessId>(
                rt => {
                    var lrt = rt.LocalCancel;
                    return Process.spawnAsync(Name, setup(lrt), inbox(lrt), Flags, Strategy, MaxMailboxSize, term(lrt), shut(lrt), System, Lazy);
                });
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
        public static Eff<RT, Seq<ProcessId>> spawnMany<S, T>(int Count,
            ProcessName Name,
            Func<Aff<RT, S>> Setup,
            Func<S, T, Aff<RT, S>> Inbox,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            Func<S, Aff<RT, Unit>> Shutdown = null,
            Func<S, ProcessId, Aff<RT, S>> Terminated = null) =>
            Range(0, Count)
               .ToSeq()
               .Sequence(n => spawn<S, T>(
                                 $"{Name}-{n}",
                                 Setup,
                                 Inbox,
                                 Flags,
                                 Strategy,
                                 MaxMailboxSize,
                                 Terminated,
                                 Shutdown));

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
        public static Aff<RT, Seq<ProcessId>> spawnMany<S, T>(ProcessName Name,
            HashMap<int, Func<Aff<RT, S>>> Spec,
            Func<S, T, Aff<RT, S>> Inbox,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            Func<S, Aff<RT, Unit>> Shutdown = null,
            Func<S, ProcessId, Aff<RT, S>> Terminated = null) =>
            Spec.AsEnumerable()
                .ToSeq()
                .Sequence(pair => spawn<S, T>($"{Name}-{pair.Key}",
                                              pair.Value,
                                              Inbox,
                                              Flags,
                                              Strategy,
                                              MaxMailboxSize,
                                              Terminated,
                                              Shutdown));
    }
}
