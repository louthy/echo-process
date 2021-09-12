using System;
using Echo.Traits;
using LanguageExt;
using Echo.ActorSys2;
using LanguageExt.Common;
using LanguageExt.Sys.Traits;
using LanguageExt.ClassInstances;
using System.Collections.Generic;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;

namespace Echo
{
    public static partial class Process<RT>  
        where RT : struct, HasEcho<RT>, HasTime<RT>
    {
        /// <summary>
        /// Log warning - Internal 
        /// </summary>
        public static Eff<RT, Unit> logWarn(string message) =>
            default(RT).EchoEff.Bind(e => e.Log(new ProcessLogItem(ProcessLogItemType.Warning, message ?? "")));

        /// <summary>
        /// Log system error - Internal 
        /// </summary>
        internal static Eff<RT, Unit> logSysErr(string message) =>
            default(RT).EchoEff.Bind(e => e.Log(new ProcessLogItem(ProcessLogItemType.SysError, message ?? "")));

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        internal static Eff<RT, Unit> logSysErr(Exception ex) =>
            default(RT).EchoEff.Bind(e => e.Log(new ProcessLogItem(ProcessLogItemType.SysError, ex)));

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        internal static Eff<RT, Unit> logSysErr(Error ex) =>
            default(RT).EchoEff.Bind(e => e.Log(new ProcessLogItem(ProcessLogItemType.SysError, ex)));

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        internal static Eff<RT, Unit> logSysErr(string message, Exception ex) =>
            default(RT).EchoEff.Bind(e => e.Log(new ProcessLogItem(ProcessLogItemType.SysError, message ?? "", ex)));

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        public static Eff<RT, Unit> logUserErr(string message) =>
            default(RT).EchoEff.Bind(e => e.Log(new ProcessLogItem(ProcessLogItemType.UserError, message ?? "")));

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static Eff<RT, Unit> logErr(Exception ex) =>
            default(RT).EchoEff.Bind(e => e.Log(new ProcessLogItem(ProcessLogItemType.Error, ex)));
        
        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static Eff<RT, Unit> logErr(Error ex) =>
            default(RT).EchoEff.Bind(e => e.Log(new ProcessLogItem(ProcessLogItemType.Error, ex)));

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static Eff<RT, Unit> logErr(string message, Exception ex) =>
            default(RT).EchoEff.Bind(e => e.Log(new ProcessLogItem(ProcessLogItemType.Error,  message ?? "", ex)));

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static Eff<RT, Unit> logErr(string message) =>
            default(RT).EchoEff.Bind(e => e.Log(new ProcessLogItem(ProcessLogItemType.Error,  message ?? "")));
    }
}