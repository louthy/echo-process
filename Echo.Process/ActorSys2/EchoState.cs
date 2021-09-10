using Echo.Traits;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2
{
    /// <summary>
    /// Encapsulates all state required to run Echo
    /// </summary>
    /// <typeparam name="RT">Runtime</typeparam>
    public class EchoState<RT>
        where RT : struct, HasEcho<RT>
    {
        public static EchoState<RT> Default = new(
            Atom(0L),
            Atom(new ActorState<RT>(ProcessId.NoSender, ProcessId.None, Empty, Strategy.Identity)),
            ActorSystems<RT>.Default,
            SystemName.None,
            SystemName.None);

        /// Actor request ID 
        readonly Atom<long> ActorRequestId;

        /// Actor state
        internal readonly Atom<ActorState<RT>> ActorState;

        /// Actor systems
        readonly ActorSystems<RT> Systems;

        /// Current system context
        readonly SystemName CurrentSystem;

        /// Default system when CurrentSystem is None 
        readonly SystemName DefaultSystem;

        EchoState(Atom<long> actorRequestId, Atom<ActorState<RT>> actorState, ActorSystems<RT> systems, SystemName currentSystem, SystemName defaultSystem)
        {
            ActorRequestId = actorRequestId;
            ActorState     = actorState;
            Systems        = systems;
            CurrentSystem  = currentSystem;
            DefaultSystem  = defaultSystem;
        }

        internal EchoState<RT> LocalEcho(Atom<ActorState<RT>> state) =>
            new EchoState<RT>(ActorRequestId, state, Systems, CurrentSystem, DefaultSystem);

        internal EchoState<RT> LocalEcho(SystemName system) =>
            new EchoState<RT>(ActorRequestId, ActorState, Systems, system, DefaultSystem);

        /// <summary>
        /// Finds the current actor.  If there isn't one, we find the User actor
        /// </summary>
        internal Fin<ActorState<RT>> GetCurrentActorState() =>
            ActorState.Value.Self.IsValid
                ? ActorState.Value
                : GetCurrentSystem().Case switch
                  {
                      ActorSystem<RT> sys => sys.FindActor(sys.Root[ActorSystemConfig.Default.UserProcessName]).Map(a => a.State),
                      _                   => ProcessError.NoSystemsRunning
                  };

        internal Fin<ActorSystem<RT>> GetCurrentSystem() =>
            Systems.FindSystem(CurrentSystem) || Systems.FindSystem(DefaultSystem) || Systems.HeadOrFail;

        internal Fin<ProcessId> Root =>
            GetCurrentSystem().Map(static sys => sys.Root);

        internal Fin<ProcessId> User =>
            Root.Map(static root => root[ActorSystemConfig.Default.UserProcessName]);

        internal Fin<ProcessId> System =>
            Root.Map(static root => root[ActorSystemConfig.Default.SystemProcessName]);

        internal long NextRequestId =>
            (long) ActorRequestId.Swap(static x => x + 1);

        internal Fin<Actor<RT>> FindActor(ProcessId pid) =>
            pid.System.IsValid
                ? Systems.FindSystem(pid.System).Bind(sys => sys.FindActor(pid))
                : ProcessError.SystemDoesNotExist(pid.System);

        internal Fin<ActorState<RT>> FindActorState(ProcessId pid) =>
            FindActor(pid).Map(static s => s.State);
        
        internal Eff<Unit> RemoveFromSystem(ProcessId pid) =>
            Systems.RemoveFromSystem(pid);
    }
}