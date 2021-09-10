using System;
using Echo.Config;
using Echo.Session;
using Echo.Traits;
using LanguageExt;
using LanguageExt.ClassInstances;
using LanguageExt.Sys.Traits;
using LanguageExt.UnitsOfMeasure;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.BuiltIn
{
    /// <summary>
    /// Supervisor of sessions
    /// </summary>
    internal static class SessionMonitorProcess<RT>
        where RT : struct, HasEcho<RT>, HasTime<RT>
    {
        public static Aff<RT, ProcessId> startup =>
            Process<RT>.spawn<Unit, (SessionSync, Time)>(ActorSystemConfig.Default.Sessions, setup, inbox);

        static Aff<RT, Unit> setup =>
            unitEff;

        static Aff<RT, Unit> inbox(Unit _, (SessionSync, Time) msg) =>
            unitEff;
    }
}