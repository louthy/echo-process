using System;
using Echo.Traits;
using LanguageExt;
using Echo.ActorSys2;
using LanguageExt.Sys.Traits;
using LanguageExt.ClassInstances;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;

namespace Echo
{
    public static partial class Process<RT>  
        where RT : struct, HasEcho<RT>, HasTime<RT>
    {
        /// <summary>
        /// Run in the provided specified system context
        /// </summary>
        public static Aff<RT, A> withSystem<A>(SystemName system, Aff<RT, A> inner) =>
            guardOutProcess.Bind(_ => localAff<RT, RT, A>(rt => rt.LocalEcho(es => es.LocalEcho(system)), inner));
        
        /// <summary>
        /// When called within a Process this returns the ID of the Process.
        /// </summary>
        public static Eff<RT, ProcessId> Self =>
            guardInProcess.Bind(static _ => getSelf);
        
        /// <summary>
        /// When called within a Process this returns the ID of the parent Process.
        /// </summary>
        public static Eff<RT, ProcessId> Parent =>
            guardInProcess.Bind(static _ => getParent);
        
        /// <summary>
        /// Returns the User process for the current system
        /// </summary>
        public static Eff<RT, ProcessId> User =>
            getUser.Map(static u => u.State.Self);
        
        /// <summary>
        /// Returns the System process for the current system
        /// </summary>
        public static Eff<RT, ProcessId> System =>
            getSystem.Map(static u => u.State.Self);
    }
}