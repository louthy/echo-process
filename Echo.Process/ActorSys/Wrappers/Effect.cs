#nullable enable
using System;
using static LanguageExt.Prelude;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Echo;

internal static class Effect
{
    public static Func<S, Unit> Shutdown<S>(Func<S, Eff<Unit>>? shutdown) =>
        shutdown == null
            ? _ => unit
            : s => shutdown(s).Run().ThrowIfFail();
    
    public static Eff<RT, Func<S, Unit>> Shutdown<RT, S>(Func<S, Eff<RT, Unit>>? shutdown) where RT : struct =>
        from rt in runtime<RT>()
        select Shutdown<RT, S>(rt, shutdown);
    
    public static Func<S, Unit> Shutdown<RT, S>(RT runtime, Func<S, Eff<RT, Unit>>? shutdown) where RT : struct =>
        shutdown == null
            ? _ => unit
            : s => shutdown(s).Run(runtime).ThrowIfFail();
    
    public static Func<S, Unit> Shutdown<S>(Func<S, Aff<Unit>>? shutdown) =>
        shutdown == null
            ? _ => unit
            : Async.Shutdown<S>(async s => (await shutdown(s).Run().ConfigureAwait(false)).ThrowIfFail());
    
    public static Eff<RT, Func<S, Unit>> Shutdown<RT, S>(Func<S, Aff<RT, Unit>>? shutdown) where RT : struct, HasCancel<RT>  =>
        from rt in runtime<RT>()
        select Shutdown<RT, S>(rt, shutdown);
    
    public static Func<S, Unit> Shutdown<RT, S>(RT runtime, Func<S, Aff<RT, Unit>>? shutdown) where RT : struct, HasCancel<RT> =>
        shutdown == null
            ? _ => unit
            : Async.Shutdown<S>(async s => (await shutdown(s).Run(runtime).ConfigureAwait(false)).ThrowIfFail());
    
    public static Func<S> Setup<S>(Eff<S> setup) =>
        () => setup.Run().ThrowIfFail();

    public static Func<S> Setup<S>(Aff<S> setup) =>
        Async.Setup(async () => (await setup.Run().ConfigureAwait(false)).ThrowIfFail());

    public static Eff<RT, Func<S>> Setup<RT, S>(Eff<RT, S> setup) where RT : struct =>
        from rt in runtime<RT>()
        select Setup<RT, S>(rt, setup);

    public static Func<S> Setup<RT, S>(RT runtime, Eff<RT, S> setup) where RT : struct =>
        () => setup.Run(runtime).ThrowIfFail();

    public static Eff<RT, Func<S>> Setup<RT, S>(Aff<RT, S> setup) where RT : struct, HasCancel<RT>  =>
        from rt in runtime<RT>()
        select Setup<RT, S>(rt, setup);
    
    public static Func<S> Setup<RT, S>(RT runtime, Aff<RT, S> setup) where RT : struct, HasCancel<RT>  =>
        Async.Setup(async () => (await setup.Run(runtime.LocalCancel).ConfigureAwait(false)).ThrowIfFail());

    public static Func<S, A, S> Inbox<S, A>(Func<S, A, Eff<S>>? inbox) =>
        inbox == null
            ? (s, _) => s
            : (s, m) => inbox(s, m).Run().ThrowIfFail();
    
    public static Func<Unit, A, Unit> Inbox<A>(Func<A, Eff<Unit>>? inbox) =>
        inbox == null
            ? (_, _) => unit
            : Inbox<Unit, A>((_, m) => inbox(m));
    
    public static Func<S, A, S> Inbox<S, A>(Func<S, A, Aff<S>>? inbox) =>
        inbox == null
            ? (s, _) => s
            : Async.Inbox<S, A>(async (s, m) => (await inbox(s, m).Run().ConfigureAwait(false)).ThrowIfFail());
    
    public static Func<Unit, A, Unit> Inbox<A>(Func<A, Aff<Unit>>? inbox) =>
        inbox == null
            ? (_, _) => unit
            : Inbox<Unit, A>((_, m) => inbox(m));

    public static Eff<RT, Func<S, A, S>> Inbox<RT, S, A>(Func<S, A, Eff<RT, S>>? inbox) where RT : struct =>
        from rt in runtime<RT>()
        select Inbox<RT, S, A>(rt, inbox);

    public static Eff<RT, Func<Unit, A, Unit>> Inbox<RT, A>(Func<A, Eff<RT, Unit>>? inbox) where RT : struct =>
        inbox == null
            ? SuccessEff<RT, Func<Unit, A, Unit>>((_, _) => unit)
            : from rt in runtime<RT>()
              select Inbox<RT, A>(rt, inbox);
    
    public static Func<S, A, S> Inbox<RT, S, A>(RT runtime, Func<S, A, Eff<RT, S>>? inbox) where RT : struct =>
        inbox == null
            ? (s, _) => s
            : (s, m) => inbox(s, m).Run(runtime).ThrowIfFail();
    
    
    public static Func<Unit, A, Unit> Inbox<RT, A>(RT runtime, Func<A, Eff<RT, Unit>>? inbox) where RT : struct =>
        inbox == null
            ? (s, _) => s
            : Inbox<RT, Unit, A>(runtime, (_, m) => inbox(m));
    
    public static Eff<RT, Func<S, A, S>> Inbox<RT, S, A>(Func<S, A, Aff<RT, S>>? inbox) where RT : struct, HasCancel<RT> =>
        from rt in runtime<RT>()
        select Inbox<RT, S, A>(rt, inbox);
    
    public static Eff<RT, Func<Unit, A, Unit>> Inbox<RT, A>(Func<A, Aff<RT, Unit>>? inbox) where RT : struct, HasCancel<RT> =>
        inbox == null
            ? SuccessEff<RT, Func<Unit, A, Unit>>((_, _) => unit)
            : from rt in runtime<RT>()
              select Inbox<RT, Unit, A>(rt, (_, m) => inbox(m));

    public static Func<S, A, S> Inbox<RT, S, A>(RT runtime, Func<S, A, Aff<RT, S>>? inbox) where RT : struct, HasCancel<RT> =>
        inbox == null
            ? (s, _) => s
            : Async.Inbox<S, A>(async (s, m) => (await inbox(s, m).Run(runtime.LocalCancel).ConfigureAwait(false)).ThrowIfFail());

    public static Func<Unit, A, Unit> Inbox<RT, A>(RT runtime, Func<A, Aff<RT, Unit>>? inbox) where RT : struct, HasCancel<RT> =>
        inbox == null
            ? (s, _) => s
            : Inbox<RT, Unit, A>(runtime, (_, m) => inbox(m));
}    

