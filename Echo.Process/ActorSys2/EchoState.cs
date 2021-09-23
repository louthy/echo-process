using System;
using Echo.ActorSys2.BuiltIn;
using Echo.Config;
using Echo.Traits;
using LanguageExt;
using LanguageExt.Sys.Traits;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2
{
    /// <summary>
    /// Encapsulates all state required to run Echo
    /// </summary>
    /// <typeparam name="RT">Runtime</typeparam>
    public class EchoState<RT>
        where RT : struct, HasEcho<RT>, HasTime<RT>
    {
        public static EchoState<RT> Default = new(Atom<HashMap<SystemName, Actor<RT>>>(Empty), SystemName.None, SystemName.None, Atom(ActorState<RT>.None));

        readonly Atom<HashMap<SystemName, Actor<RT>>> Systems;
        readonly SystemName DefaultSystem;
        readonly Atom<ActorState<RT>> ActorState;
        readonly SystemName CurrentSystem;

        /// <summary>
        /// True if we're in the user's inbox or setup function
        /// </summary>
        internal bool InProcess =>
            ActorState.Value.Self.IsValid;

        /// <summary>
        /// Ctor
        /// </summary>
        EchoState(Atom<HashMap<SystemName, Actor<RT>>> systems, SystemName defaultSystem, SystemName currentSystem, Atom<ActorState<RT>> actorState) =>
            (Systems, DefaultSystem, CurrentSystem, ActorState) = (systems, defaultSystem, currentSystem, actorState);

        /// <summary>
        /// Create a local context
        /// </summary>
        internal EchoState<RT> LocalEcho(Atom<ActorState<RT>> state) =>
            new EchoState<RT>(Systems, DefaultSystem, state.Value.Self.System, state);

        /// <summary>
        /// Create a local context
        /// </summary>
        internal EchoState<RT> LocalEcho(SystemName system) =>
            new EchoState<RT>(Systems, DefaultSystem, system, ActorState);
        
        /// <summary>
        /// Start a new actor system
        /// </summary>
        /// <param name="name">Name of the system</param>
        /// <param name="rootProcessName">Name of the root process</param>
        /// <param name="config">Configuration</param>
        /// <returns></returns>
        internal Aff<RT, Unit> StartSystem(SystemName name, ProcessName rootProcessName, ProcessSystemConfig config) =>
            
            // Reserve a slot in Systems for us
            // We do this because we don't want multiple invocations of side-effecting operations within a swap.  This 
            // is side-effect free and just takes the slot we'll need once the root-process has been bootstrapped.
            from _1 in Systems.SwapEff(sys => sys.ContainsKey(name)
                                                  ? FailEff<HashMap<SystemName, Actor<RT>>>(ProcessError.SystemAlreadyExists(name))
                                                  : SuccessEff(sys = sys.AddOrUpdate(name, Actor<RT>.None)))
        
            // Bootstrap the root process
            from sn in BuiltIn.RootProcess<RT>.bootstrap(name, rootProcessName, config) | SuccessEff(Actor<RT>.None)

            // Take the reserved slot if the bootstrapping of the root process succeeded, 
            // otherwise free up the reserved slot
            let _ = Systems.Swap(sys => sn.IsNone ? sys.Remove(name) : sys.AddOrUpdate(name, sn))
            
            select unit;

        /// <summary>
        /// Tell a message to a process
        /// </summary>
        /// <param name="pid">Process to tell</param>
        /// <param name="post">Message to tell</param>
        internal Eff<RT, Unit> Tell(ProcessId pid, Post post) =>
            Systems.Value
                   .Find(pid.System)
                   .ToEff(ProcessError.SystemDoesNotExist(pid.System))
                   .Bind(sys => Tell(sys, pid, post));

        /// <summary>
        /// Recursively walks the process ID looking for the actor to post the message to
        /// </summary>
        /// <param name="ctx">Current actor</param>
        /// <param name="pid">Process ID to tell</param>
        /// <param name="post">Message to post</param>
        /// <returns></returns>
        static Eff<RT, Unit> Tell(Actor<RT> ctx, ProcessId pid, Post post) =>
            pid.Count() switch
            {
                0 => FailEff<Unit>(ProcessError.ProcessDoesNotExist(pid)),
                1 => ctx.State
                        .Children
                        .Find(pid.Name)
                        .ToEff(ProcessError.ProcessDoesNotExist(pid))
                        .Bind(c => post switch
                                   {
                                        UserPost up => c.User(up),
                                        SysPost sp  => c.Sys(sp),
                                        _           => unitEff
                                   }),
                _ => ctx.State
                        .Children
                        .Find(pid.HeadName())
                        .ToEff(ProcessError.ProcessDoesNotExist(pid))
                        .Bind(nctx => Tell(nctx, pid.Tail(), post))
            };
        

        /// <summary>
        /// Finds the current actor.  If there isn't one, we find the User actor
        /// </summary>
        internal Fin<ActorState<RT>> GetCurrentActorState() =>
            ActorState.Value.Self.IsValid
                ? ActorState.Value
                : FinFail<ActorState<RT>>(ProcessError.MustBeCalledWithinProcessContext);

        internal Unit ModifyCurrentActorState(Func<ActorState<RT>, ActorState<RT>> f) =>
            ignore(ActorState.Swap(f));
        
        internal Fin<Actor<RT>> GetCurrentSystem() =>
            (Systems.Value.Find(CurrentSystem) || 
             Systems.Value.Find(DefaultSystem) || 
             Systems.Value.Values.HeadOrNone())
           .ToFin(ProcessError.NoSystemsRunning);
    }
}