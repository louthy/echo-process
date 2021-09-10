using System;
using Echo.Config;
using Echo.Traits;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Sys.Traits;
using LanguageExt.ClassInstances;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.BuiltIn
{
    /// <summary>
    /// Supervisor of errors
    /// </summary>
    internal static class ErrorsProcess<RT>
        where RT : struct, HasEcho<RT>, HasTime<RT>
    {
        public static Aff<RT, ProcessId> startup =>
            Process<RT>.spawn<Unit, Error>(ActorSystemConfig.Default.ErrorsProcessName, setup, inbox);

        static Aff<RT, Unit> setup =>
            unitEff;

        static Aff<RT, Unit> inbox(Unit _, Error msg) =>
            unitEff;
    }
}