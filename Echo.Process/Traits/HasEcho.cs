using System;
using System.Collections.Generic;
using Echo.ActorSys2;
using LanguageExt;
using LanguageExt.ClassInstances;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys.Traits;
using static LanguageExt.Prelude;

namespace Echo.Traits
{
    public interface EchoIO<RT>
        where RT : struct, HasEcho<RT>
    {
        /// <summary>
        /// Watch the `watched` process for termination, then run the termination-inbox in the watcher 
        /// </summary>
        Aff<RT, Unit> Watch(ProcessId watcher, ProcessId watched);

        /// <summary>
        /// Un-watch the `watched` process for termination 
        /// </summary>
        Aff<RT, Unit> UnWatch(ProcessId watcher, ProcessId watched);

        /// <summary>
        /// Forward to dead-letters
        /// </summary>
        /// <param name="post">Dead letter</param>
        Aff<RT, Unit> ForwardToDeadLetters(Post post);

        /// <summary>
        /// Tell a message to a process
        /// </summary>
        Aff<RT, Unit> Tell(ProcessId pid, Post msg);

        /// <summary>
        /// Tell a message to a process
        /// </summary>
        Aff<RT, Unit> TellMany(IEnumerable<ProcessId> pids, Post msg);

        /// <summary>
        /// Logging 
        /// </summary>
        Eff<RT, Unit> Log(ProcessLogItem item);
    }

    public interface HasEcho<RT> : HasCancel<RT>, HasTime<RT>
        where RT : struct, HasEcho<RT>
    {
        /// <summary>
        /// All state required to run Echo
        /// </summary>
        EchoState<RT> EchoState { get; }

        /// <summary>
        /// Map the EchoState within the runtime into a new runtime
        /// </summary>
        RT LocalEcho(Func<EchoState<RT>, EchoState<RT>> f);

        /// <summary>
        /// Get the Echo interface
        /// </summary>
        Eff<RT, EchoIO<RT>> EchoEff { get; }
    }
}