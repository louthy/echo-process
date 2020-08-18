using LanguageExt;
using LanguageExt.UnitsOfMeasure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Echo
{
    internal interface IActor : IDisposable
    {
        /// <summary>
        /// Process path
        /// </summary>
        ProcessId Id { get; }

        /// <summary>
        /// Process name
        /// </summary>
        ProcessName Name { get; }

        /// <summary>
        /// Parent process
        /// </summary>
        ActorItem Parent { get; }

        /// <summary>
        /// Flags
        /// </summary>
        ProcessFlags Flags { get; }

        /// <summary>
        /// Failure strategy
        /// </summary>
        State<StrategyContext, Unit> Strategy { get; }

        /// <summary>
        /// Child processes
        /// </summary>
        HashMap<string, ActorItem> Children { get; }

        /// <summary>
        /// Clears the state (keeps the mailbox items)
        /// </summary>
        /// <param name="unpauseAfterRestart">if set to true then inbox shall be unpaused after starting up again</param>
        Aff<RT, Unit> Restart<RT>(bool unpauseAfterRestart) where RT : struct, HasEcho<RT>;

        /// <summary>
        /// Startup
        /// </summary>
        /// <returns>returns InboxDirective.Pause if Startup will unpause inbox (via some strategy error handling). Otherwise InboxDirective.Default</returns>
        Aff<RT, InboxDirective> Startup<RT>() where RT : struct, HasEcho<RT>;

        /// <summary>
        /// Shutdown
        /// </summary>
        Aff<RT, Unit> Shutdown<RT>(bool maintainState) where RT : struct, HasEcho<RT>;

        /// <summary>
        /// Link child
        /// </summary>
        /// <param name="pid">Child to link</param>
        Aff<RT, Unit> LinkChild<RT>(ActorItem pid) where RT : struct, HasEcho<RT>;

        /// <summary>
        /// Unlink child
        /// </summary>
        /// <param name="pid">Child to unlink</param>
        Aff<RT, Unit> UnlinkChild<RT>(ProcessId pid) where RT : struct, HasEcho<RT>;

        /// <summary>
        /// Add a watcher of this Process
        /// </summary>
        /// <param name="pid">Id of the Process that will watch this Process</param>
        Aff<RT, Unit> AddWatcher<RT>(ProcessId pid) where RT : struct, HasEcho<RT>;

        /// <summary>
        /// Remove a watcher of this Process
        /// </summary>
        /// <param name="pid">Id of the Process that will stop watching this Process</param>
        Aff<RT, Unit> RemoveWatcher<RT>(ProcessId pid) where RT : struct, HasEcho<RT>;

        /// <summary>
        /// Publish to the PublishStream
        /// </summary>
        Aff<RT, Unit> Publish<RT>(object message) where RT : struct, HasEcho<RT>;

        /// <summary>
        /// Publish stream - for calls to Process.pub
        /// </summary>
        Eff<RT, IObservable<object>> PublishStream<RT>() where RT : struct, HasEcho<RT>;

        /// <summary>
        /// State stream - sent after each message loop
        /// </summary>
        Eff<RT, IObservable<object>> StateStream<RT>() where RT : struct, HasEcho<RT>;

        Aff<RT, InboxDirective> ProcessMessage<RT>(object message) where RT : struct, HasEcho<RT>;
        Aff<RT, InboxDirective> ProcessAsk<RT>(ActorRequest request) where RT : struct, HasEcho<RT>;
        Aff<RT, InboxDirective> ProcessTerminated<RT>(ProcessId id) where RT : struct, HasEcho<RT>;

        Aff<RT, R> ProcessRequest<RT, R>(ProcessId pid, object message) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> ProcessResponse<RT>(ActorResponse response) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> ShutdownProcess<RT>(bool maintainState) where RT : struct, HasEcho<RT>;

        Aff<RT, Unit> AddSubscription<RT>(ProcessId pid, IDisposable sub) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> RemoveSubscription<RT>(ProcessId pid) where RT : struct, HasEcho<RT>;

        Aff<RT, Unit> DispatchWatch<RT>(ProcessId pid) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> DispatchUnWatch<RT>(ProcessId pid) where RT : struct, HasEcho<RT>;
    }
}
