using Echo.Config;
using Echo.Traits;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2
{
    /// <summary>
    /// Actor system
    /// </summary>
    internal record ActorSystem<RT>(SystemName Name, ProcessSystemConfig Config, Atom<HashMap<ProcessId, Actor<RT>>> Actors, ProcessId Root)
        where RT : struct, HasEcho<RT>
    {
        /// <summary>
        /// Create a new actor system
        /// </summary>
        /// <param name="name">Name of the system</param>
        /// <param name="config">Config</param>
        /// <param name="rootProcessName">Root process name</param>
        /// <returns></returns>
        public static ActorSystem<RT> New(SystemName name, ProcessSystemConfig config, ProcessName rootProcessName) =>
            new ActorSystem<RT>(name, config, Atom(HashMap<ProcessId, Actor<RT>>()), ProcessId.Top.Child(rootProcessName).SetSystem(name));
        
        /// <summary>
        /// Add an actor
        /// </summary>
        /// <param name="pid">Actor ID</param>
        /// <param name="actor">Actor</param>
        /// <returns>New ActorSystem or Error</returns>
        public Eff<Unit> AddActor(ProcessId pid, Actor<RT> actor) =>
            Actors.SwapEff(a => a.ContainsKey(pid)
                                   ? FailEff<HashMap<ProcessId, Actor<RT>>>(ProcessError.ProcessAlreadyExists(pid))
                                   : a.ContainsKey(pid.Parent)
                                       ? FailEff<HashMap<ProcessId, Actor<RT>>>(ProcessError.ProcessParentDoesNotExist(pid))
                                       : SuccessEff(a.Add(pid, actor)))
                  .Map(static _ => unit);

        /// <summary>
        /// Remove an actor
        /// </summary>
        public Eff<Unit> RemoveActor(ProcessId pid) =>
            Actors.SwapEff(a => a.ContainsKey(pid)
                                    ? SuccessEff(a.Remove(pid))
                                    : FailEff<HashMap<ProcessId, Actor<RT>>>(ProcessError.ProcessDoesNotExist(pid)))
                  .Map(static _ => unit);
        
        /// <summary>
        /// Find an actor
        /// </summary>
        public Fin<Actor<RT>> FindActor(ProcessId pid) =>
            Actors.Value.Find(pid).ToFin(default) || ProcessError.ProcessDoesNotExist(pid);
    }
}