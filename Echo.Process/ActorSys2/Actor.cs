using System;
using Echo.Traits;
using LanguageExt;
using LanguageExt.Sys;
using System.Threading;
using LanguageExt.Pipes;
using LanguageExt.Common;
using LanguageExt.Sys.Traits;
using LanguageExt.TypeClasses;
using System.Reactive.Subjects;
using System.Threading.Channels;
using LanguageExt.ClassInstances;
using System.Collections.Generic;
using static LanguageExt.Prelude;
using static LanguageExt.Pipes.Proxy;

namespace Echo.ActorSys2
{
    /// <summary>
    /// Instance of an actor
    /// </summary>
    internal record Actor<RT>(
        ProcessName Name,
        Func<UserPost, Eff<Unit>> User,
        Func<SysPost, Eff<Unit>> Sys,
        Aff<RT, Unit> Effect,
        ActorState<RT> State)
        where RT : struct, HasEcho<RT>
    {
        public static readonly Actor<RT> None = new Actor<RT>(
            default, 
            _ => unitEff, 
            _ => unitEff, 
            unitEff, 
            ActorState<RT>.None);

        public bool IsNone =>
            !Name.IsValid;
    }
}