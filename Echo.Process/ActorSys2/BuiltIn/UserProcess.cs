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
    /// Supervisor of user-land processes
    /// </summary>
    internal static class UserProcess<RT>
        where RT : struct, HasEcho<RT>, HasTime<RT>
    {
        public static Aff<RT, ProcessId> startup =>
            Process<RT>.spawn<Unit, SysMessage>(ActorSystemConfig.Default.UserProcessName, setup, inbox);

        static Aff<RT, Unit> setup =>
            unitEff;

        static Aff<RT, Unit> inbox(Unit _, SysMessage msg) =>
            unitEff;
    }
}