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
        public static Aff<RT, ProcessId> startup =>
            Process<RT>.spawn<ClusterMonitor.State, ClusterMonitor.Msg>(ActorSystemConfig.Default.MonitorProcessName, setup, inbox);

        static Aff<RT, ClusterMonitor.State> setup =>
            unitEff;

        static Aff<RT, ClusterMonitor.State> inbox(ClusterMonitor.State state, ClusterMonitor.Msg msg) =>
            unitEff;
    }
}