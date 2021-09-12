using System;
using Echo.Config;
using Echo.Traits;
using LanguageExt;
using LanguageExt.ClassInstances;
using LanguageExt.Sys.Traits;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.BuiltIn
{
    internal static class RootProcess<RT>
        where RT : struct, HasEcho<RT>, HasTime<RT>
    {
        /// <summary>
        /// Bootstrap the whole process-system by creating the root process
        /// </summary>
        public static Aff<RT, Actor<RT>> bootstrap(SystemName system, ProcessName rootProcessName, ProcessSystemConfig config) =>
            from root in SuccessEff(Actor<RT, EqDefault<RootProcessState>, RootProcessState, SysMessage>
                                       .make(
                                            ProcessId.Top.Child(rootProcessName).SetSystem(system),
                                            ProcessId.Top.Child(rootProcessName).SetSystem(system),
                                            setup(system, config),
                                            inbox,
                                            shutdown,
                                            terminated,
                                            100,
                                            Strategy.Identity))
            from canc in fork(root.Effect)
            from _ in root.User(new UserPost(ProcessId.NoSender, SysMessage.Bootstrap, 0))
            select root;

        /// <summary>
        /// Root process setup
        /// </summary>
        static Func<Aff<RT, RootProcessState>> setup(SystemName system, ProcessSystemConfig config) =>
            () => SuccessEff(new RootProcessNotStarted(system, config) as RootProcessState);

        /// <summary>
        /// Root process inbox
        /// </summary>
        static Aff<RT, RootProcessState> inbox(RootProcessState state, SysMessage msg) =>
            (state, msg) switch
            {
                (RootProcessNotStarted, BootstrapMsg) => from s in SystemProcess<RT>.startup
                                                         from u in UserProcess<RT>.startup
                                                         select RootProcessState.Running(state.System, state.Config),
                
                _ => SuccessEff(state)  // TODO: Forward to children
            };

        /// <summary>
        /// Actor system shut down
        /// </summary>
        static Aff<RT, Unit> shutdown(RootProcessState _) =>
            unitEff;

        /// <summary>
        /// A watched process has shutdown
        /// </summary>
        static Aff<RT, RootProcessState> terminated(RootProcessState state, ProcessId terminatedProcess) =>
            SuccessEff(state);  
    }

    /// <summary>
    /// State of the root process
    /// </summary>
    internal abstract record RootProcessState(SystemName System, ProcessSystemConfig Config)
    {
        /// <summary>
        /// Root process hasn't yet spawned its children
        /// </summary>
        public static RootProcessState NotStarted(SystemName System, ProcessSystemConfig Config) =>
            new RootProcessNotStarted(System, Config);

        /// <summary>
        /// Root process has spawned its children
        /// </summary>
        public static RootProcessState Running(SystemName System, ProcessSystemConfig Config) =>
            new RootProcessRunning(System, Config);
    }

    /// <summary>
    /// Root process hasn't yet spawned its children
    /// </summary>
    internal record RootProcessNotStarted(SystemName System, ProcessSystemConfig Config) : RootProcessState(System, Config);
    
    /// <summary>
    /// Root process has spawned its children
    /// </summary>
    internal record RootProcessRunning(SystemName System, ProcessSystemConfig Config) : RootProcessState(System, Config);
}