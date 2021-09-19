using System;
using Echo.Traits;
using LanguageExt;
using Echo.ActorSys2;
using LanguageExt.Sys.Traits;
using LanguageExt.ClassInstances;
using System.Collections.Generic;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;

namespace Echo
{
    public static partial class Process<RT>  
        where RT : struct, HasEcho<RT>, HasTime<RT>
    {
        /// <summary>
        /// Un-watch the `watched` process for termination 
        /// </summary>
        internal static Aff<RT, Unit> forwardToDeadLetters(Post post) =>
            default(RT).EchoEff.Bind(e => e.ForwardToDeadLetters(post));
 
        /// <summary>
        /// Tell a message to a process
        /// </summary>
        internal static Aff<RT, Unit> post(ProcessId pid, Post msg) =>
            default(RT).EchoEff.Bind(e => e.Tell(pid, msg));
        
        /// <summary>
        /// Tell a message to a process
        /// </summary>
        internal static Aff<RT, Unit> postMany(IEnumerable<ProcessId> pids, Post msg) =>
            default(RT).EchoEff.Bind(e => e.TellMany(pids, msg));

        /// <summary>
        /// Get the actor's state
        /// </summary>
        internal static Eff<RT, ActorState<RT>> get =>
            echoState.Bind(static es => es.GetCurrentActorState().ToEff());

        /// <summary>
        /// Get the strategy computation
        /// </summary>
        internal static Eff<RT, State<StrategyContext, Unit>> getStrategy =>
            get.Map(static a => a.Strategy);

        /// <summary>
        /// Get the ProcessId of this Actor
        /// </summary>
        internal static Eff<RT, ProcessId> getSelf =>
            get.Map(static a => a.Self);

        /// <summary>
        /// Get the ProcessId of this Actor
        /// </summary>
        internal static Eff<RT, ProcessId> getParent =>
            get.Map(static a => a.Parent);

        /// <summary>
        /// Children of this Actor
        /// </summary>
        internal static Eff<RT, HashMap<ProcessName, Actor<RT>>> getChildren =>
            get.Map(static a => a.Children);

        /// <summary>
        /// Get the current system
        /// </summary>
        internal static Eff<RT, Actor<RT>> getCurrentSystem =>
            echoState.Bind(static es => es.GetCurrentSystem().ToEff());

        /// <summary>
        /// Make sure we're running in a process 
        /// </summary>
        static Eff<RT, Unit> guardInProcess =>
            echoState.Bind(es => es.InProcess
                                     ? FailEff<Unit>(ProcessError.MustBeCalledWithinProcessContext)
                                     : unitEff);

        /// <summary>
        /// Protect against running in a process 
        /// </summary>
        static Eff<RT, Unit> guardOutProcess =>
            echoState.Bind(es => es.InProcess
                                     ? unitEff
                                     : FailEff<Unit>(ProcessError.MustBeCalledOutsideProcessContext));

        static Eff<RT, EchoState<RT>> echoState =>
            Eff<RT, EchoState<RT>>(rt => rt.EchoState);
    }
}