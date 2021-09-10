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
    /// Supervisor of javascript processes
    /// </summary>
    internal static class JsProcess<RT>
        where RT : struct, HasEcho<RT>, HasTime<RT>
    {
        public static Aff<RT, ProcessId> startup =>
            Process<RT>.spawn<Unit, RelayMsg>("js", setup, inbox);

        static Aff<RT, Unit> setup =>
            unitEff;

        static Aff<RT, Unit> inbox(Unit _, RelayMsg msg) =>
            unitEff;
    }
}