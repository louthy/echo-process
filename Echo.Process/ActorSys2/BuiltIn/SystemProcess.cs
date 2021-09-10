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
    /// Supervisor of system-land processes
    /// </summary>
    internal static class SystemProcess<RT>
        where RT : struct, HasEcho<RT>, HasTime<RT>
    {
        public static Aff<RT, ProcessId> startup =>
            Process<RT>.spawn<Unit, SysMessage>(ActorSystemConfig.Default.SystemProcessName, setup, inbox);

        static Aff<RT, Unit> setup =>
            from _1 in SessionMonitorProcess<RT>.startup
            from _2 in DeadLettersProcess<RT>.startup
            from _3 in ErrorsProcess<RT>.startup
            from _4 in ClusterMonitorProcess<RT>.startup
            select unit;

        static Aff<RT, Unit> inbox(Unit _, SysMessage msg) =>
            unitEff;
    }
}