using Echo.Config;
using Echo.Traits;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2
{
    /// <summary>
    /// Collection of actor systems
    /// </summary>
    internal record ActorSystems<RT>(Atom<HashMap<SystemName, ActorSystem<RT>>> Systems)
        where RT : struct, HasEcho<RT>
    {
        public static ActorSystems<RT> Default = new (Atom(HashMap<SystemName, ActorSystem<RT>>()));

        /// <summary>
        /// Add an actor system
        /// </summary>
        public Eff<Unit> AddSystem(SystemName name, ProcessName rootProcessName, ProcessSystemConfig config) =>
            AddSystem(name, ActorSystem<RT>.New(name, config, rootProcessName));
        
        /// <summary>
        /// Add an actor system
        /// </summary>
        public Eff<Unit> AddSystem(SystemName name, ActorSystem<RT> system) =>
            Systems.SwapEff(s => s.ContainsKey(name)
                                     ? FailEff<HashMap<SystemName, ActorSystem<RT>>>(ProcessError.SystemAlreadyExists(name))
                                     : SuccessEff(s.Add(name, system)))
                   .Map(static _ => unit);

        /// <summary>
        /// Remove an actor system
        /// </summary>
        public Eff<Unit> RemoveSystem(SystemName name) =>
            Systems.SwapEff(s => s.ContainsKey(name)
                                     ? SuccessEff(s.Remove(name))
                                     : FailEff<HashMap<SystemName, ActorSystem<RT>>>(ProcessError.SystemDoesNotExist(name)))
                   .Map(static _ => unit);

        /// <summary>
        /// Find a system
        /// </summary>
        public Fin<ActorSystem<RT>> FindSystem(SystemName name) =>
            Systems.Value.Find(name).ToFin(default) || ProcessError.SystemDoesNotExist(name);
        
        /// <summary>
        /// Find the first system
        /// </summary>
        public Fin<ActorSystem<RT>> HeadOrFail =>
            Systems.Value.Values.HeadOrNone().ToFin(default) || ProcessError.NoSystemsRunning;

        /// <summary>
        /// Remove a process from the system
        /// </summary>
        public Eff<Unit> RemoveFromSystem(ProcessId pid) =>
            FindSystem(pid.System)
               .ToEff()
               .Bind(sys => sys.RemoveActor(pid));
    }
}