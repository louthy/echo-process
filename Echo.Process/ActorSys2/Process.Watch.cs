using System;
using Echo.Traits;
using LanguageExt;
using Echo.ActorSys2;
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
        /// Watch the `watched` process for termination, then run the termination-inbox in the watcher 
        /// </summary>
        public static Aff<RT, Unit> watch(ProcessId watcher, ProcessId watched) =>
            default(RT).EchoEff.Bind(e => e.Watch(watcher, watched));
        
        /// <summary>
        /// Un-watch the `watched` process for termination 
        /// </summary>
        public static Aff<RT, Unit> unwatch(ProcessId watcher, ProcessId watched) =>
            default(RT).EchoEff.Bind(e => e.UnWatch(watcher, watched));
    }
}