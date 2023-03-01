using LanguageExt;
using System;
using System.Linq;
using System.Threading.Tasks;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;

namespace Echo;

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
    where RT : struct, HasCancel<RT>
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
    /// <returns>A ProcessId that identifies the child</returns>
    public static Eff<RT, ProcessId> spawn<T>(
        ProcessName Name,
        Func<T, Aff<RT, Unit>> Inbox,
        ProcessFlags Flags = ProcessFlags.Default,
        State<StrategyContext, Unit> Strategy = null,
        int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
        Func<ProcessId, Aff<RT, Unit>> Terminated = null) =>
        from ibx in Effect.Inbox(Inbox)
        from trm in Effect.Inbox(Terminated)
        select Process.spawn(Name, () => unit, ibx, Flags, Strategy, MaxMailboxSize, trm);

    /// <summary>
    /// Create a new process by name (accepts Unit as a return value instead of void).  
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
    /// <returns>A ProcessId that identifies the child</returns>
    public static Eff<RT, ProcessId> spawnUnit<T>(
        ProcessName Name,
        Func<T, Aff<RT, Unit>> Inbox,
        ProcessFlags Flags = ProcessFlags.Default,
        State<StrategyContext, Unit> Strategy = null,
        int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
        Func<ProcessId, Aff<RT, Unit>> Terminated = null) =>
        from ibx in Effect.Inbox(Inbox)
        from trm in Effect.Inbox(Terminated)
        select Process.spawn(Name, () => unit, ibx, Flags, Strategy, MaxMailboxSize, trm);
    
    /// <summary>
    /// Create a new process by name.  
    /// If this is called from within a process' message loop 
    /// then the new process will be a child of the current process.  If it is called from
    /// outside of a process, then it will be made a child of the root 'user' process.
    /// </summary>
    /// <typeparam name="T">Type of messages that the child-process can accept</typeparam>
    /// <param name="Name">Name of the child-process</param>
    /// <param name="Setup">Startup and restart function</param>
    /// <param name="Inbox">Function that is the process</param>
    /// <param name="Flags">Process flags</param>
    /// <param name="Strategy">Failure supervision strategy</param>
    /// <param name="MaxMailboxSize">Maximum number of messages that can back up in the process</param>
    /// <param name="Terminated">Message function to call when a Process [that this Process
    /// watches] terminates</param>
    /// <param name="Shutdown">Process to call on shutdown</param>
    /// <param name="Lazy">If set to true actor will not start automatically, you need to
    /// startup(processId) manually</param>
    /// <returns>A ProcessId that identifies the child</returns>
    public static Eff<RT, ProcessId> spawn<S, T>(
        ProcessName Name,
        Aff<RT, S> Setup,
        Func<S, T, Aff<RT, S>> Inbox,
        ProcessFlags Flags = ProcessFlags.Default,
        State<StrategyContext, Unit> Strategy = null,
        int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
        Func<S, ProcessId, Aff<RT, S>> Terminated = null,
        Func<S, Aff<RT, Unit>> Shutdown = null,
        bool Lazy = false) =>
        from sup in Effect.Setup(Setup)
        from ibx in Effect.Inbox(Inbox)
        from trm in Effect.Inbox(Terminated)
        select Process.spawn(Name, sup, ibx, Flags, Strategy, MaxMailboxSize, trm);

    /// <summary>
    /// Create N child processes.
    /// The name provided will be used as a basis to generate the child names.  Each child will
    /// be named "name-index" where index starts at zero.  
    /// If this is called from within a process' message loop 
    /// then the new processes will be a children of the current process.  If it is called from
    /// outside of a process, then they will be made a child of the root 'user' process.
    /// </summary>
    /// <typeparam name="T">Type of messages that the child-process can accept</typeparam>
    /// <param name="Count">Number of processes to spawn</param>
    /// <param name="Name">Name of the child-process</param>
    /// <param name="Inbox">Function that is the process</param>
    /// <param name="Flags">Process flags</param>
    /// <param name="Strategy">Failure supervision strategy</param>
    /// <param name="MaxMailboxSize">Maximum inbox size</param>
    /// <param name="Terminated">Message function to call when a Process [that this Process
    /// watches] terminates</param>
    /// <returns>ProcessId IEnumerable</returns>
    public static Eff<RT, Seq<ProcessId>> spawnMany<T>(
        int Count, 
        ProcessName Name, 
        Func<T, Aff<RT, Unit>> Inbox, 
        ProcessFlags Flags = ProcessFlags.Default,
        State<StrategyContext, Unit> Strategy = null,
        int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
        Func<ProcessId, Aff<RT, Unit>> Terminated = null) =>
        from ibx in Effect.Inbox(Inbox)
        from trm in Effect.Inbox(Terminated)
        select Process.spawnMany(Count, Name, () => unit, ibx, Flags, Strategy, MaxMailboxSize, null, trm).ToSeq();

    /// <summary>
    /// Create N child processes.
    /// The name provided will be used as a basis to generate the child names.  Each child will
    /// be named "name-index" where index starts at zero.  
    /// If this is called from within a process' message loop 
    /// then the new processes will be a children of the current process.  If it is called from
    /// outside of a process, then they will be made a child of the root 'user' process.
    /// </summary>
    /// <typeparam name="T">Type of messages that the child-process can accept</typeparam>
    /// <param name="Count">Number of processes to spawn</param>
    /// <param name="Setup">Startup and restart function</param>
    /// <param name="Name">Name of the child-process</param>
    /// <param name="Inbox">Function that is the process</param>
    /// <param name="Flags">Process flags</param>
    /// <param name="Strategy">Failure supervision strategy</param>
    /// <param name="MaxMailboxSize">Maximum inbox size</param>
    /// <param name="Terminated">Message function to call when a Process [that this Process
    /// watches] terminates</param>
    /// <returns>ProcessId IEnumerable</returns>
    public static Eff<RT, Seq<ProcessId>> spawnMany<S, T>(
        int Count, 
        ProcessName Name, 
        Aff<RT, S> Setup, 
        Func<S, T, Aff<RT, S>> Inbox, 
        ProcessFlags Flags = ProcessFlags.Default,
        State<StrategyContext, Unit> Strategy = null,
        int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
        Func<S, Aff<RT, Unit>> Shutdown = null,
        Func<S, ProcessId, Aff<RT, S>> Terminated = null) =>
        from sup in Effect.Setup(Setup)
        from ibx in Effect.Inbox(Inbox)
        from sdn in Effect.Shutdown(Shutdown)
        from trm in Effect.Inbox(Terminated)
        select Process.spawnMany(Count, Name, sup, ibx, Flags, Strategy, MaxMailboxSize, sdn, trm).ToSeq();

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
    /// <param name="Terminated">Message function to call when a Process [that this Process
    /// watches] terminates</param>
    /// <returns>ProcessId IEnumerable</returns>
    public static Eff<RT, Seq<ProcessId>> spawnMany<S, T>(
        ProcessName Name,
        HashMap<int, Aff<RT, S>> Spec, 
        Func<S, T, Aff<RT, S>> Inbox, 
        ProcessFlags Flags = ProcessFlags.Default,
        State<StrategyContext, Unit> Strategy = null,
        int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
        Func<S, Aff<RT, Unit>> Shutdown = null,
        Func<S, ProcessId, Aff<RT, S>> Terminated = null) =>
        from rt in runtime<RT>()
        from ibx in Effect.Inbox(Inbox)
        from sdn in Effect.Shutdown(Shutdown)
        from trm in Effect.Inbox(Terminated)
        select Process.spawnMany(Name, Spec.Map(x => Effect.Setup(rt, x)), ibx, Flags, Strategy, MaxMailboxSize, sdn, trm).ToSeq();
}
