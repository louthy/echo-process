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
    internal interface IActor
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
        /// Link child
        /// </summary>
        /// <param name="pid">Child to link</param>
        EffPure<Unit> LinkChild(ActorItem<RT> pid);

        /// <summary>
        /// Unlink child
        /// </summary>
        /// <param name="pid">Child to unlink</param>
        EffPure<Unit> UnlinkChild(ProcessId pid);

        /// <summary>
        /// Publish to the PublishStream
        /// </summary>
        EffPure<Unit> Publish(object message);

        /// <summary>
        /// Publish stream - for calls to Process.publish
        /// </summary>
        IObservable<object> PublishStream { get; }

        /// <summary>
        /// State stream - sent after each message loop
        /// </summary>
        IObservable<object> StateStream { get; }

        /// <summary>
        /// Add a subscription
        /// </summary>
        /// <remarks>If one already exists then it is safely disposed</remarks>
        EffPure<Unit> AddSubscription(ProcessId pid, IDisposable sub);
        
        /// <summary>
        /// Remove a subscription and safely dispose it 
        /// </summary>
        EffPure<Unit> RemoveSubscription(ProcessId pid);
        
        /// <summary>
        /// Safely dispose and remove all subscriptions
        /// </summary>
        /// <returns></returns>
        EffPure<Unit> RemoveAllSubscriptions();

        /// <summary>
        /// Clear the strategy state, so it has 0 retries, back-off, etc.
        /// </summary>
        EffPure<Unit> ResetStrategyState();
        
        /// <summary>
        /// Update the strategy state
        /// </summary>
        EffPure<Unit> SetStrategyState(StrategyState state);

        /// <summary>
        /// Current state for the strategy system
        /// </summary>
        StrategyState StrategyState { get; }

        IActor<RT> WithRuntime<RT>() where RT : struct, HasEcho<RT>;
    }

    internal interface IActor<RT> : IActor
        where RT : struct, HasEcho<RT>
    {
        /// <summary>
        /// Restarts the process (and shutdowns down all children)
        /// Clears the state, but keeps the mailbox items
        /// </summary>
        /// <param name="unpauseAfterRestart">if set to true then inbox shall be unpaused after starting up again</param>
        Aff<RT, Unit> Restart(bool unpauseAfterRestart);

        /// <summary>
        /// Startup
        /// </summary>
        /// <returns>returns InboxDirective.Pause if Startup will unpause inbox (via some strategy error handling). Otherwise InboxDirective.Default</returns>
        Aff<RT, InboxDirective> Startup { get; }

        /// <summary>
        /// Shutdown everything from this process and all its child processes
        /// </summary>
        Aff<RT, Unit> Shutdown(bool maintainState);
        /// <summary>
        /// Add a watcher of this Process
        /// </summary>
        /// <param name="pid">Id of the Process that will watch this Process</param>
        Eff<RT, Unit> AddWatcher(ProcessId pid);

        /// <summary>
        /// Remove a watcher of this Process
        /// </summary>
        /// <param name="pid">Id of the Process that will stop watching this Process</param>
        Eff<RT, Unit> RemoveWatcher(ProcessId pid);

        Aff<RT, InboxDirective> ProcessTell { get; }
        Aff<RT, InboxDirective> ProcessAsk { get; }
        Aff<RT, InboxDirective> ProcessTerminated(ProcessId id);

        Aff<RT, R> ProcessRequest<R>(ProcessId pid, object message);
        Aff<RT, Unit> ProcessResponse(ActorResponse response);

        Aff<RT, Unit> DispatchWatch(ProcessId pid);
        Aff<RT, Unit> DispatchUnWatch(ProcessId pid);
    }
}
