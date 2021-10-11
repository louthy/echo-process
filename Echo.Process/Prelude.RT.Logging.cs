using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using Echo.Traits;
using LanguageExt;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;

namespace Echo
{
    public static partial class Process<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        /// <summary>
        /// Log warning 
        /// </summary>
        public static Aff<RT, Unit> logWarn(string message) =>
            Eff(() => Process.logWarn(message));

        /// <summary>
        /// Log user error 
        /// </summary>
        public static Aff<RT, Unit> logUserErr(string message) =>
            Eff(() => Process.logUserErr(message));

        /// <summary>
        /// Log user or system error 
        /// </summary>
        public static Aff<RT, Unit> logErr(Exception ex) =>
            Eff(() => Process.logErr(ex));

        /// <summary>
        /// Log user or system error 
        /// </summary>
        public static Aff<RT, Unit> logErr(string message, Exception ex) =>
            Eff(() => Process.logErr(message, ex));

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static Aff<RT, Unit> logErr(string message) =>
            Eff(() => Process.logErr(message));
    }
}
