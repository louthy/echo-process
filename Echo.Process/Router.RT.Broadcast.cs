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
        /// Spawns a new process with Count worker processes, each message is sent to 
        /// all worker processes
        /// </summary>
        /// <typeparam name="S">State type</typeparam>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="Name">Delegator process name</param>
        /// <param name="Count">Number of worker processes</param>
        /// <param name="Inbox">Worker message handler</param>
        /// <param name="Setup">Setup effect</param>
        /// <param name="Flags">Process flags</param>
        /// <param name="Strategy">Failure supervision strategy</param>
        /// <param name="MaxMailboxSize">Max mailbox size</param>
        /// <param name="WorkerName">Name of the worker</param>
        /// <returns>Process ID of the delegator process</returns>
        public static Aff<RT, ProcessId> broadcast<S, T>(
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

            return Process<RT>.spawn<Unit, T>(
                Name,
                Process<RT>.spawnMany(Count, WorkerName, Setup, Inbox, Flags).Map(static _ => unit),
                (_, msg) => from cs in Process<RT>.Children
                            from rt in cs.Values.SequenceParallel(Process<RT>.fwd).Map(static _ => unit)
                            select rt,
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
        public static Aff<RT, ProcessId> broadcast<T>(ProcessName Name,
            Seq<ProcessId> Workers,
            RouterOption Options = RouterOption.Default,
            ProcessFlags Flags = ProcessFlags.Default,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize) =>
            Eff(() => Router.broadcast<T>(Name, Workers, Options, Flags, MaxMailboxSize));

        /// <summary>
        /// Spawns a new process with Count worker processes, each message is sent to 
        /// all worker processes
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
        public static Aff<RT, ProcessId> broadcastMap<S, T, U>(
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

            return Process<RT>.spawn<Unit, T>(
                Name,
                Process<RT>.spawnMany(Count, WorkerName, Setup, Inbox, Flags).Map(static _ => unit),
                (x, msg) => from u in SuccessEff(Map(msg))
                            from cs in Process<RT>.Children
                            from rt in cs.Values.SequenceParallel(p => Process<RT>.fwd(p, u))
                            select unit,
                Flags,
                Strategy,
                MaxMailboxSize
            );
        }

        /// <summary>
        /// Spawns a new process with that routes each message to all workers
        /// Each message is mapped before being broadcast.
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
        public static Aff<RT, ProcessId> broadcastMap<T, U>(
            ProcessName Name,
            IEnumerable<ProcessId> Workers,
            Func<T, U> Map,
            RouterOption Options = RouterOption.Default,
            ProcessFlags Flags = ProcessFlags.Default,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize) =>
            Eff(() => Router.broadcastMap<T, U>(Name, Workers, Map, Options, Flags, MaxMailboxSize));
        
        /// <summary>
        /// Spawns a new process with N worker processes, each message is mapped 
        /// from T to IEnumerable U before each resulting Us are passed all of the 
        /// worker processes.
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
        public static Aff<RT, ProcessId> broadcastMapMany<S, T, U>(
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

            return Process<RT>.spawn<Unit, T>(
                Name,
                Process<RT>.spawnMany(Count, WorkerName, Setup, Inbox, Flags).Map(static _ => unit),
                (x, msg) => MapMany(msg).SequenceParallel(u => from cs in Process<RT>.Children
                                                               from rt in cs.Values.SequenceParallel(pid => Process<RT>.fwd(pid, u))
                                                               select rt)
                                        .Map(_ => unit),
                Flags,
                Strategy,
                MaxMailboxSize
            );
        }

        /// <summary>
        /// Spawns a new process with N worker processes, each message is mapped 
        /// from T to IEnumerable U before each resulting Us are passed all of the 
        /// worker processes.
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
        public static Aff<RT, ProcessId> broadcastMapMany<T, U>(
            ProcessName Name,
            IEnumerable<ProcessId> Workers,
            Func<T, IEnumerable<U>> MapMany,
            RouterOption Options = RouterOption.Default,
            ProcessFlags Flags = ProcessFlags.Default,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize) =>
            Eff(() => Router.broadcastMapMany<T, U>(Name, Workers, MapMany, Options, Flags, MaxMailboxSize));

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
        public static Aff<RT, ProcessId> broadcast<T>(
            ProcessName Name,
            int Count,
            Func<T, Aff<RT, Unit>> Inbox,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            string WorkerName = "worker") =>
            broadcast<Unit, T>(Name, Count, unitAff, (_, msg) => Inbox(msg), Flags, Strategy, MaxMailboxSize, WorkerName);

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
        public static Aff<RT, ProcessId> broadcastMap<T, U>(
            ProcessName Name,
            int Count,
            Func<U, Aff<RT, Unit>> Inbox,
            Func<T, U> Map,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            string WorkerName = "worker") =>
            broadcastMap<Unit, T, U>(Name, Count, unitAff, (_, umsg) => Inbox(umsg), Map, Flags, Strategy, MaxMailboxSize, WorkerName);

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
        public static Aff<RT, ProcessId> broadcastMapMany<T, U>(
            ProcessName Name,
            int Count,
            Func<U, Aff<RT, Unit>> Inbox,
            Func<T, IEnumerable<U>> MapMany,
            ProcessFlags Flags = ProcessFlags.Default,
            State<StrategyContext, Unit> Strategy = null,
            int MaxMailboxSize = ProcessSetting.DefaultMailboxSize,
            string WorkerName = "worker") =>
            broadcastMapMany<Unit, T, U>(Name, Count, unitAff, (_, umsg) => Inbox(umsg), MapMany, Flags, Strategy, MaxMailboxSize, WorkerName);
    }
}
