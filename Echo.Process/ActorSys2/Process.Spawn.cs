using System;
using Echo.Traits;
using LanguageExt;
using Echo.ActorSys2;
using LanguageExt.Sys.Traits;
using LanguageExt.ClassInstances;
using System.Collections.Generic;
using LanguageExt.Effects.Traits;
using LanguageExt.Pipes;
using static LanguageExt.Prelude;

namespace Echo
{
    public static partial class Process<RT>  
        where RT : struct, HasEcho<RT>, HasTime<RT>
    {
        /// <summary>
        /// Create a new process by name.  
        /// If this is called from within a process' message loop 
        /// then the new process will be a child of the current process.  If it is called from
        /// outside of a process, then it will be made a child of the root 'user' process.
        /// </summary>
        /// <typeparam name="S">Type of state the process maintains</typeparam>
        /// <typeparam name="A">Type of messages that the child-process can accept</typeparam>
        /// <param name="Name">Name of the child-process</param>
        /// <param name="Setup">Startup and restart function</param>
        /// <param name="Inbox">Function that is the process</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <param name="MaxMailboxSize">Maximum size of the process's inbox before it starts dropping messages</param>
        /// <param name="Shutdown">Function to call when the process is shutting down</param>
        /// <param name="Terminated">Message function to call when a Process [that this Process
        /// watches] terminates</param>
        /// <param name="Lazy">If set to true actor will not start automatically, you need to
        /// startup(processId) manually</param>
        /// <returns>A ProcessId that identifies the child</returns>
        public static Aff<RT, ProcessId> spawn<S, A>(
            ProcessName Name,
            Aff<RT, S> Setup,
            Func<S, A, Aff<RT, S>> Inbox,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit>? Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            Func<S, ProcessId, Aff<RT, S>>? Terminated = null,
            Func<S, Aff<RT, Unit>>? Shutdown = null,
            bool Lazy = false) =>
                // Find out who we are
                from parent in getSelf
                let child = parent.Child(Name)

                // Make the child actor
                let actor = Actor<RT, EqDefault<S>, S, A>.make(
                                    child,
                                    parent,
                                    Setup,
                                    Inbox,
                                    Shutdown ?? (_ => unitEff),
                                    Terminated ?? ((s, _) => SuccessEff(s)),
                                    Consumer.awaiting<RT, S>()
                                            .Map(static _ => unit)
                                            .ToConsumer(),
                                    MaxMailboxSize,
                                    Strategy ?? Process.DefaultStrategy)
                
                // Link it to the current actor
                from _ in post(parent, new LinkChildSysPost<RT>(actor, parent))
                select child;

        /// <summary>
        /// Create a new process by name.  
        /// If this is called from within a process' message loop 
        /// then the new process will be a child of the current process.  If it is called from
        /// outside of a process, then it will be made a child of the root 'user' process.
        /// </summary>
        /// <typeparam name="A">Type of messages that the child-process can accept</typeparam>
        /// <param name="Name">Name of the child-process</param>
        /// <param name="Inbox">Function that is the process</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <param name="MaxMailboxSize">Maximum size of the process's inbox before it starts dropping messages</param>
        /// <param name="Shutdown">Function to call when the process is shutting down</param>
        /// <param name="Terminated">Message function to call when a Process [that this Process
        /// watches] terminates</param>
        /// <param name="Lazy">If set to true actor will not start automatically, you need to
        /// startup(processId) manually</param>
        /// <returns>A ProcessId that identifies the child</returns>
        public static Aff<RT, ProcessId> spawn<A>(
            ProcessName Name,
            Func<A, Aff<RT, Unit>> Inbox,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit>? Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            Func<ProcessId, Aff<RT, Unit>>? Terminated = null,
            Aff<RT, Unit>? Shutdown = null,
            bool Lazy = false) =>
            spawn<Unit, A>(
                Name,
                unitEff,
                (_, m) => Inbox(m),
                Flags,
                Strategy,
                MaxMailboxSize,
                (_, id) => Terminated?.Invoke(id) ?? unitEff,
                _ => Shutdown ?? unitEff,
                Lazy);
    }
}