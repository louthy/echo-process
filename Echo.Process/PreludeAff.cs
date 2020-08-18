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
    }
}