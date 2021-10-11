using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Echo.Traits;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;

namespace Echo
{
    public static partial class Router<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        /// <summary>
        /// Spawns a new process with Count worker processes, each message is sent to one worker
        /// process in a round robin fashion.
        /// </summary>
        /// <typeparam name="S">State type</typeparam>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Setup">Setup effect</param>
        /// <param name="Count">Number of worker processes</param>
        /// <param name="Inbox">Worker message handler</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <param name="MaxMailboxSize">Max mailbox size</param>
        /// <param name="WorkerName">Name of the worker</param>
        /// <returns>Process ID of the delegator process</returns>
        public static Aff<RT, ProcessId> roundRobin<S, T>(
            ProcessName Name,
            int Count,
            Aff<RT, S> Setup,
            Func<S, T, Aff<RT, S>> Inbox,
            ProcessFlags Flags                    = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize                    = ProcessSetting.DefaultMailboxSize,
            string WorkerName                     = "worker"
            )
        {
            if (Inbox == null) throw new ArgumentNullException(nameof(Inbox));
            if (Count < 1) throw new ArgumentException($"{nameof(Count)} should be greater than 0");

            return Process<RT>.spawn<int, T>(
                Name,
                Process<RT>.spawnMany(Count, WorkerName, Setup, Inbox, Flags).Map(static _ => 0),
                (index, msg) => from cs in Process<RT>.Children
                                let mindex = index % cs.Count
                                let child = cs.Values.Skip(mindex).HeadOrNone()
                                from rt in child.Case switch
                                           {
                                               ProcessId pid => Process<RT>.fwd(pid),
                                               _             => throw new NoRouterWorkersException()
                                           }
                                select mindex + 1, 
                Flags, 
                Strategy, 
                MaxMailboxSize
            );
        }

        /// <summary>
        /// Spawns a new process with that routes each message to the Workers
        /// in a round robin fashion.
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Workers">Worker processes</param>
        /// <param name="Options">Router options</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="MaxMailboxSize">Max mailbox size</param>
        /// <returns>Process ID of the delegator process</returns>
        public static Aff<RT, ProcessId> roundRobin<T>(
            ProcessName Name,
            IEnumerable<ProcessId> Workers,
            RouterOption Options = RouterOption.Default,
            ProcessFlags Flags = ProcessFlags.Default,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize) =>
            Eff(() => Router.roundRobin<T>(Name, Workers, Options, Flags, MaxMailboxSize)); 

        /// <summary>
        /// Spawns a new process with Count worker processes, each message is mapped
        /// and sent to one worker process in a round robin fashion.
        /// </summary>
        /// <typeparam name="S">State type</typeparam>
        /// <typeparam name="T">Message type</typeparam>
        /// <typeparam name="U">Mapped message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Map">Message mapping function</param>
        /// <param name="Count">Number of worker processes</param>
        /// <param name="Setup">Setup effect</param>
        /// <param name="Inbox">Worker message handler</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <param name="MaxMailboxSize">Max mailbox size</param>
        /// <param name="WorkerName">Name of the worker</param>
        /// <returns>Process ID of the delegator process</returns>
        public static Aff<RT, ProcessId> roundRobinMap<S, T, U>(
            ProcessName Name,
            int Count,
            Aff<RT, S> Setup,
            Func<S, U, Aff<RT, S>> Inbox,
            Func<T, U> Map,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            string WorkerName = "worker"
            )
        {
            if (Inbox == null) throw new ArgumentNullException(nameof(Inbox));
            if (Count < 1) throw new ArgumentException($"{nameof(Count)} should be greater than 0");

            return Process<RT>.spawn<int, T>(
                Name,
                Process<RT>.spawnMany(Count, WorkerName, Setup, Inbox, Flags).Map(_ => 0),
                (index, msg) => from cs in Process<RT>.Children
                                let u = Map(msg)
                                let ix = index % cs.Count
                                let ch = cs.Values.Skip(ix).HeadOrNone()
                                from rt in ch.Case switch
                                           {
                                               ProcessId pid => Process<RT>.fwd(pid, u),
                                               _             => throw new NoRouterWorkersException() 
                                           }
                                select ix + 1,
                Flags,
                Strategy,
                MaxMailboxSize
            );
        }

        /// <summary>
        /// Spawns a new process with that routes each message is mapped and 
        /// sent to the Workers in a round robin fashion.
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <typeparam name="U">Mapped message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Map">Message mapping function</param>
        /// <param name="Workers">Worker processes</param>
        /// <param name="Options">Router options</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="MaxMailboxSize">Max mailbox size</param>
        /// <returns>Process ID of the delegator process</returns>
        public static Aff<RT, ProcessId> roundRobinMap<T, U>(
            ProcessName Name,
            IEnumerable<ProcessId> Workers,
            Func<T, U> Map,
            RouterOption Options = RouterOption.Default,
            ProcessFlags Flags = ProcessFlags.Default,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize) =>
            Eff(() => Router.roundRobinMap<T, U>(Name, Workers, Map, Options, Flags, MaxMailboxSize));

        /// <summary>
        /// Spawns a new process with N worker processes, each message is mapped 
        /// from T to IEnumerable U before each resulting U is passed to the worker
        /// processes in a round robin fashion.
        /// </summary>
        /// <typeparam name="S">State type</typeparam>
        /// <typeparam name="T">Message type</typeparam>
        /// <typeparam name="U">Mapped message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="MapMany">Message mapping function</param>
        /// <param name="Count">Number of worker processes</param>
        /// <param name="Setup">Setup effect</param>
        /// <param name="Inbox">Worker message handler</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <param name="MaxMailboxSize">Max mailbox size</param>
        /// <param name="WorkerName">Name of the worker</param>
        /// <returns>Process ID of the delegator process</returns>
        public static Aff<RT, ProcessId> roundRobinMapMany<S, T, U>(
            ProcessName Name,
            int Count,
            Aff<RT, S> Setup,
            Func<S, U, Aff<RT, S>> Inbox,
            Func<T, IEnumerable<U>> MapMany,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            string WorkerName = "worker"
            )
        {
            if (Inbox == null) throw new ArgumentNullException(nameof(Inbox));
            if (WorkerName == null) throw new ArgumentNullException(nameof(WorkerName));
            if (Count < 1) throw new ArgumentException($"{nameof(Count)} should be greater than 0");

            return Process<RT>.spawn<int, T>(
                Name,
                Process<RT>.spawnMany(Count, WorkerName, Setup, Inbox, Flags).Map(static _ => 0),
                (index, msg) => from cs in Process<RT>.Children.Map(static cs => cs.Values.ToSeq())
                                let us = MapMany(msg)
                                let ix1 = index % cs.Count
                                from rt in foreverChild(cs.Skip(ix1) + cs.Take(ix1))
                                                .Zip(us)
                                                .SequenceParallel(cm => Process<RT>.fwd(cm.Item1, cm.Item2))
                                select ix1 + rt.Count(),
                Flags,
                Strategy,
                MaxMailboxSize);
            
            static IEnumerable<ProcessId> foreverChild(Seq<ProcessId> ids)
            {
                while (true)
                    foreach (var id in ids) yield return id;
            }
        }


        /// <summary>
        /// Spawns a new process with N worker processes, each message is mapped 
        /// from T to IEnumerable U before each resulting U is passed to the worker
        /// processes in a round robin fashion.
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <typeparam name="U">Mapped message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="MapMany">Message mapping function</param>
        /// <param name="Workers">Worker processes</param>
        /// <param name="Options">Router options</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="MaxMailboxSize">Max mailbox size</param>
        /// <returns>Process ID of the delegator process</returns>
        public static Aff<RT, ProcessId> roundRobinMapMany<T, U>(
            ProcessName Name,
            IEnumerable<ProcessId> Workers,
            Func<T, IEnumerable<U>> MapMany,
            RouterOption Options = RouterOption.Default,
            ProcessFlags Flags = ProcessFlags.Default,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize) =>
            Eff(() => Router.roundRobinMapMany<T, U>(Name, Workers, MapMany, Options, Flags, MaxMailboxSize));

        /// <summary>
        /// Spawns a new process with Count worker processes, each message is sent to one worker
        /// process in a round robin fashion.
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Count">Number of worker processes</param>
        /// <param name="Inbox">Worker message handler</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <param name="MaxMailboxSize">Max mailbox size</param>
        /// <param name="WorkerName">Name of the worker</param>
        /// <returns>Process ID of the delegator process</returns>
        public static Aff<RT, ProcessId> roundRobin<T>(
            ProcessName Name,
            int Count,
            Func<T, Aff<RT, Unit>> Inbox,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            string WorkerName = "worker") =>
            roundRobin<Unit, T>(Name, Count, unitAff, (_, msg) => Inbox(msg), Flags, Strategy, MaxMailboxSize, WorkerName);

        /// <summary>
        /// Spawns a new process with Count worker processes, each message is mapped
        /// and sent to one worker process in a round robin fashion.
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <typeparam name="U">Mapped message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Map">Message mapping function</param>
        /// <param name="Count">Number of worker processes</param>
        /// <param name="Inbox">Worker message handler</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <param name="MaxMailboxSize">Max mailbox size</param>
        /// <param name="WorkerName">Name of the worker</param>
        /// <returns>Process ID of the delegator process</returns>
        public static Aff<RT, ProcessId> roundRobinMap<T, U>(
            ProcessName Name,
            int Count,
            Func<U, Aff<RT, Unit>> Inbox,
            Func<T, U> Map,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            string WorkerName = "worker") =>
            roundRobinMap<Unit, T, U>(Name, Count, unitAff, (_, umsg) => Inbox(umsg), Map, Flags, Strategy, MaxMailboxSize, WorkerName);

        /// <summary>
        /// Spawns a new process with N worker processes, each message is mapped 
        /// from T to IEnumerable U before each resulting U is passed to the worker
        /// processes in a round robin fashion.
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <typeparam name="U">Mapped message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Count">Number of worker processes</param>
        /// <param name="MapMany">Maps the message from T to IEnumerable U before each one is passed to the workers</param>
        /// <param name="Inbox">Worker message handler</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <param name="MaxMailboxSize">Max mailbox size</param>
        /// <param name="WorkerName">Name of the worker</param>
        /// <returns>Process ID of the delegator process</returns>
        public static Aff<RT, ProcessId> roundRobinMapMany<T, U>(
            ProcessName Name,
            int Count,
            Func<U, Aff<RT, Unit>> Inbox,
            Func<T, IEnumerable<U>> MapMany,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            string WorkerName = "worker") =>
            roundRobinMapMany<Unit, T, U>(Name, Count, unitAff, (_, umsg) => Inbox(umsg), MapMany, Flags, Strategy, MaxMailboxSize, WorkerName);
    }
}
