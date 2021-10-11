using System;
using System.Collections.Generic;
using System.Linq;
using Echo.Traits;
using static LanguageExt.Prelude;
using static Echo.Process;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Echo
{
    public class Dispatch<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        /// <summary>
        /// Registers a dispatcher for a role
        /// Dispatchers take in a 'leaf' ProcessId (i.e. /user/my-process) and return an enumerable
        /// of real ProcessIds that the Process system will use to deliver all of the standard functions
        /// like tell, ask, subscribe, etc.
        /// </summary>
        /// <param name="name">Name of the dispatcher</param>
        /// <param name="selector">Function that will be invoked every time a dispatcher based ProcessId
        /// is used.</param>
        /// <returns>A root dispatcher ProcessId.  Use this to create new ProcessIds that will
        /// be passed to the selector function whenever the dispatcher based ProcessId is used</returns>
        public static Eff<RT, ProcessId> register(ProcessName name, Func<ProcessId, IEnumerable<ProcessId>> selector) =>
            Eff(() => Dispatch.register(name, selector));

        /// <summary>
        /// Removes the dispatcher registration for the named dispatcher
        /// </summary>
        /// <param name="name">Name of the dispatcher to remove</param>
        public static Eff<RT, Unit> deregister(ProcessName name) =>
            Eff(() => Dispatch.deregister(name));

        /// <summary>
        /// Builds a ProcessId that represents a set of Processes.  When used for
        /// operations like 'tell', the message is dispatched to all Processes in 
        /// the set.
        /// </summary>
        /// <example>
        ///     tell( Dispatch.broadcast(pid1,pid2,pid3), "Hello" );
        /// </example>
        public static ProcessId broadcast(IEnumerable<ProcessId> processIds) =>
            Dispatch.broadcast(processIds);

        /// <summary>
        /// Builds a ProcessId that represents a set of Processes.  When used 
        /// for operations like 'tell', the message is dispatched to the least busy
        /// Process from the set.
        /// </summary>
        /// <example>
        ///     tell( Dispatch.leastBusy(pid1,pid2,pid3), "Hello" );
        /// </example>
        public static ProcessId leastBusy(IEnumerable<ProcessId> processIds) =>
            Dispatch.leastBusy(processIds);

        /// <summary>
        /// Builds a ProcessId that represents a set of Processes.  When used 
        /// for operations like 'tell', the message is dispatched to a cryptographically
        /// random Process from the set.
        /// </summary>
        /// <example>
        ///     tell( Dispatch.random(pid1,pid2,pid3), "Hello" );
        /// </example>
        public static ProcessId random(IEnumerable<ProcessId> processIds) =>
            Dispatch.random(processIds);

        /// <summary>
        /// Builds a ProcessId that represents a set of Processes.  When used 
        /// for operations like 'tell', the message is dispatched to the Processes in a 
        /// round-robin fashion
        /// </summary>
        /// <example>
        ///     tell( Dispatch.roundRobin(pid1,pid2,pid3), "Hello" );
        /// </example>
        public static ProcessId roundRobin(IEnumerable<ProcessId> processIds) =>
            Dispatch.roundRobin(processIds);

        /// <summary>
        /// Builds a ProcessId that represents a set of Processes.  When used for
        /// operations like 'tell', the message is dispatched to all Processes in 
        /// the set.
        /// </summary>
        /// <example>
        ///     tell( Dispatch.broadcast(pid1,pid2,pid3), "Hello" );
        /// </example>
        public static ProcessId broadcast(ProcessId processId, params ProcessId[] processIds) =>
            Dispatch.broadcast(processId, processIds);

        /// <summary>
        /// Builds a ProcessId that represents a set of Processes.  When used 
        /// for operations like 'tell', the message is dispatched to the least busy
        /// Process from the set.
        /// </summary>
        /// <example>
        ///     tell( Dispatch.leastBusy(pid1,pid2,pid3), "Hello" );
        /// </example>
        public static ProcessId leastBusy(ProcessId processId, params ProcessId[] processIds) =>
            Dispatch.leastBusy(processId, processIds);

        /// <summary>
        /// Builds a ProcessId that represents a set of Processes.  When used 
        /// for operations like 'tell', the message is dispatched to a cryptographically
        /// random Process from the set.
        /// </summary>
        /// <example>
        ///     tell( Dispatch.random(pid1,pid2,pid3), "Hello" );
        /// </example>
        public static ProcessId random(ProcessId processId, params ProcessId[] processIds) =>
            Dispatch.random(processId, processIds);

        /// <summary>
        /// Builds a ProcessId that represents a set of Processes.  When used 
        /// for operations like 'tell', the message is dispatched to the Processes in a 
        /// round-robin fashion
        /// </summary>
        /// <example>
        ///     tell( Dispatch.roundRobin(pid1,pid2,pid3), "Hello" );
        /// </example>
        public static ProcessId roundRobin(ProcessId processId, params ProcessId[] processIds) =>
            Dispatch.roundRobin(processId, processIds);
    }
}
