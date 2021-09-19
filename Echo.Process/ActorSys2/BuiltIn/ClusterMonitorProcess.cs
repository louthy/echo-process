using System;
using Echo.Config;
using Echo.Traits;
using LanguageExt;
using LanguageExt.ClassInstances;
using LanguageExt.Sys.Traits;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.BuiltIn
{
    /// <summary>
    /// Supervisor of the cluster
    /// </summary>
    internal static class ClusterMonitorProcess<RT>
        where RT : struct, HasEcho<RT>, HasTime<RT>
    {
        public record State(HashMap<ProcessName, ClusterNode> Members);

        public static Aff<RT, ProcessId> startup =>
            Process<RT>.spawn<State, ClusterMonitor.Msg>(ActorSystemConfig.Default.MonitorProcessName, setup, inbox);

        static Aff<RT, State> setup =>
            SuccessEff(new State(Empty));

        static Aff<RT, State> inbox(State state, ClusterMonitor.Msg msg) =>
            SuccessEff(state);
    }
}