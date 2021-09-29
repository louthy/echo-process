using System;
using System.Diagnostics;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.Configuration
{
    public record Context(
        ContextBindings<TmBinding> TmBindings, 
        ContextBindings<TyBinding> TyBindings, 
        Lst<Term> Store)
    {
        public static readonly Context Empty = new(
            new ContextBindings<TmBinding>(default, ProcessError.UndefinedVariable, ProcessError.VariableAlreadyExists),
            new ContextBindings<TyBinding>(default, ProcessError.UndefinedType, ProcessError.TypeAlreadyExists),
            default);

        public static Context<Unit> log<A>(A value) =>
            new Context<Unit>(ctx => {
                                  Show(value);
                                  return FinSucc((unit, ctx));
                              });

        static Unit Show<A>(A value)
        {
            if (value != null) Console.WriteLine(value);
            return unit;
        }

        /// <summary>
        /// Context success
        /// </summary>
        public static Context<A> Pure<A>(A value) =>
            new Context<A>(ctx => FinSucc((value, ctx)));

        /// <summary>
        /// Context true
        /// </summary>
        public static readonly Context<bool> False =
            Pure(false);

        /// <summary>
        /// Context false
        /// </summary>
        public static readonly Context<bool> True =
            Pure(true);

        /// <summary>
        /// Context unit
        /// </summary>
        public static readonly Context<Unit> Unit =
            Pure(unit);

        public static readonly Context<Term> NoRuleAppliesTerm =
            Fail<Term>(ProcessError.NoRuleApplies);

        public static readonly Context<Ty> NoRuleAppliesTy =
            Fail<Ty>(ProcessError.NoRuleApplies);

        public static readonly Context<Kind> StarKind =
            Pure<Kind>(Kind.Star);
        
        /// <summary>
        /// Context fail
        /// </summary>
        public static Context<A> Fail<A>(Error error) =>
            new Context<A>(_ => FinFail<(A, Context)>(error));
        
        /// <summary>
        /// Create a local context and run `ma` in it
        /// </summary>
        public static Context<A> local<A>(Func<Context, Context> f, Context<A> ma) =>
            new Context<A>(ctx => { 
                               var ra = ma.Op(f(ctx));
                               return ra.Map(p => (p.Item1, ctx));
                           });
        
        /// <summary>
        /// Add a local binding to run `ma` in
        /// </summary>
        public static Context<A> localBinding<A>(string name, TyBinding binding, Context<A> ma) =>
            new Context<A>(ctx => {
                               var ra = ma.Op(ctx with {TyBindings = ctx.TyBindings.AddLocal(name, binding)});
                               return ra.Map(p => (p.Item1, ctx));
                           });
        
        /// <summary>
        /// Add a local binding to run `ma` in
        /// </summary>
        public static Context<A> localBinding<A>(string name, TmBinding binding, Context<A> ma) =>
            new Context<A>(ctx => {
                               var ra = ma.Op(ctx with {TmBindings = ctx.TmBindings.AddLocal(name, binding)});
                               return ra.Map(p => (p.Item1, ctx));
                           });

        /// <summary>
        /// Get the context
        /// </summary>
        public static Context<Context> get =>
            new Context<Context>(static ctx => FinSucc((ctx, ctx)));

        /// <summary>
        /// Modify the context
        /// </summary>
        public static Context<Unit> modify(Func<Context, Fin<Context>> f) =>
            new Context<Unit>(ctx => f(ctx).Map(nctx => (unit, nctx)));

        /// <summary>
        /// Modify the context
        /// </summary>
        public static Context<Unit> modify(Func<Context, Context> f) =>
            new Context<Unit>(ctx => (unit, f(ctx)));

        /// <summary>
        /// Modify the type bindings
        /// </summary>
        public static Context<Unit> modifyTys(Func<ContextBindings<TyBinding>, Fin<ContextBindings<TyBinding>>> f) =>
            new Context<Unit>(ctx => f(ctx.TyBindings).Map(nctx => (unit, ctx with { TyBindings = nctx })));

        /// <summary>
        /// Modify the type bindings
        /// </summary>
        public static Context<Unit> modifyTys(Func<ContextBindings<TyBinding>, ContextBindings<TyBinding>> f) =>
            new Context<Unit>(ctx => (unit, ctx with {TyBindings = f(ctx.TyBindings)}));

        /// <summary>
        /// Modify the term bindings
        /// </summary>
        public static Context<Unit> modifyTms(Func<ContextBindings<TmBinding>, Fin<ContextBindings<TmBinding>>> f) =>
            new Context<Unit>(ctx => f(ctx.TmBindings).Map(nctx => (unit, ctx with {TmBindings = nctx})));

        /// <summary>
        /// Modify the term bindings
        /// </summary>
        public static Context<Unit> modifyTms(Func<ContextBindings<TmBinding>, ContextBindings<TmBinding>> f) =>
            new Context<Unit>(ctx => (unit, ctx with {TmBindings = f(ctx.TmBindings)}));

        /// <summary>
        /// Get binding
        /// </summary>
        public static Context<TyBinding> getTyBinding(Loc loc, string name) =>
            get.Bind(ctx => ctx.TyBindings.GetBinding(loc, name).ToContext());

        /// <summary>
        /// Get binding
        /// </summary>
        public static Context<TmBinding> getTmBinding(Loc loc, string name) =>
            get.Bind(ctx => ctx.TmBindings.GetBinding(loc, name).ToContext());

        /// <summary>
        /// Add a top level binding
        /// </summary>
        public static Context<Unit> addTop(Loc loc, string name, TyBinding b) =>
            modifyTys(ctx => ctx.AddTop(loc, name, b));

        /// <summary>
        /// Add a top level binding
        /// </summary>
        public static Context<Unit> addTop(Loc loc, string name, TmBinding b) =>
            modifyTms(ctx => ctx.AddTop(loc, name, b));

        /// <summary>
        /// Add a term to the store
        /// </summary>
        public static Context<int> extendStore(Term term) =>
            from _ in modify(ctx => ctx.ExtendStore(term))
            from ix in get.Map(ctx => ctx.Store.Count - 1)
            select ix;

        /// <summary>
        /// Update store
        /// </summary>
        public static Context<Unit> updateStore(int ix, Term term) =>
            modify(ctx => ctx.UpdateStore(ix, term));

        /// <summary>
        /// Get item from store
        /// </summary>
        public static Context<Term> lookupLoc(int ix) =>
            get.Bind(ctx => ix < ctx.Store.Count
                            ? Context.Pure(ctx.Store[ix])
                            : Context.Fail<Term>(Error.New("store: out-of-range")));

        /// <summary>
        /// Get type of the binding
        /// </summary>
        public static Context<Ty> getTmType(Loc loc, string name) =>
            from b in getTmBinding(loc, name)
            from t in b switch
                      {
                          TmVarBind (var ty)                              => Pure(ty),
                          TmAbbBind (_, var oty) when oty.Case is (Ty ty) => Pure(ty),
                          TmAbbBind (_, var oty)          Case            => Fail<Ty>(ProcessError.NoTypeRecordedForVariable(loc, name)),
                          _                                               => Fail<Ty>(ProcessError.WrongTypeOfBindingForVariable(loc, name))
                      }
            select t;

        /// <summary>
        /// Get kind of the binding 
        /// </summary>
        public static Context<Kind> getKind(Loc loc, string name) =>
            from b in getTyBinding(loc, name)
            from k in b.GetKind(loc, name).ToContext()
            select k;

        public static Context<Unit> checkKindStar(Loc loc, Ty ty) =>
            from k in ty.KindOf(loc)
            from r in k == Kind.Star ? Unit : Fail<Unit>(ProcessError.StarKindExpected(loc))
            select r;

        public static Context<bool> isTmNameBound(string name) =>
            get.Map(c => c.TmBindings.IsNameBound(name));

        public static Context<bool> isTyNameBound(string name) =>
            get.Map(c => c.TyBindings.IsNameBound(name));

        /// <summary>
        /// Is the binding a type-lambda
        /// </summary>
        public static Context<bool> isTyLam(string name) =>
            getTyBinding(Loc.None, name).Map(b => b is TyLamBind);

        /// <summary>
        /// Get the binding if it's a type-lambda
        /// </summary>
        public static Context<Ty> getTyLam(string name) =>
            getTyBinding(Loc.None, name).Bind(b => b is TyLamBind ab ? Context.Pure(ab.Type) :  Context.NoRuleAppliesTy);

        /// <summary>
        /// Add a term to the store
        /// </summary>
        public Context ExtendStore(Term term) =>
            this with {Store = Store.Add(term)};

        /// <summary>
        /// Update the store
        /// </summary>
        public Context UpdateStore(int ix, Term term) =>
            this with {Store = Store.SetItem(ix, term)};

        public override string ToString() => "Context";
    }

    public record ContextBindings<A>(
        HashMap<string, A> Bindings, 
        Func<Loc, string, Error> Undefined, 
        Func<Loc, string, Error> AlreadyExists)
    {
        /// <summary>
        /// Get the binding
        /// </summary>
        public Fin<A> GetBinding(Loc loc, string name) =>
            Bindings.Find(name).ToFin(default) || Undefined(loc, name);
        
        /// <summary>
        /// Add a top level binding
        /// </summary>
        public Fin<ContextBindings<A>> AddTop(Loc loc, string name, A b) =>
            Bindings.ContainsKey(name)
                ? AlreadyExists(loc, name)
                : this with {Bindings = Bindings.Add(name, b)};

        /// <summary>
        /// Add a local binding
        /// </summary>
        public ContextBindings<A> AddLocal(string name, A b) =>
            this with {Bindings = Bindings.AddOrUpdate(name, b)};

        /// <summary>
        /// Check if a name is bound
        /// </summary>
        public bool IsNameBound(string name) =>
            Bindings.ContainsKey(name);

        /// <summary>
        /// Find a unique name from an existing name
        /// </summary>
        public string PickFreshName(string name) =>
            IsNameBound(name)
                ? PickFreshName($"{name}'")
                : name;
        
        /// <summary>
        /// Find binding
        /// </summary>
        public Option<A> Find(string name) =>
            Bindings.Find(name);
    }

    public class Context<A>
    {
        internal readonly Func<Context, Fin<(A Value, Context Context)>> Op;

        internal Context(Func<Context, Fin<(A Value, Context Context)>> op) =>
            Op = op;

        public Fin<(A Value, Context Context)> Run(Context context) =>
            Op(context);

        public Context<B> Select<B>(Func<A, B> f) =>
            new Context<B>(ctx => Op(ctx).Map(p => (f(p.Value), p.Context)));

        public Context<B> Map<B>(Func<A, B> f) =>
            new Context<B>(ctx => Op(ctx).Map(p => (f(p.Value), p.Context)));

        public Context<B> SelectMany<B>(Func<A, Context<B>> f) =>
            new Context<B>(ctx => Op(ctx).Bind(a => f(a.Value).Op(a.Context)));

        public Context<B> Bind<B>(Func<A, Context<B>> f) =>
            new Context<B>(ctx => Op(ctx).Bind(a => f(a.Value).Op(a.Context)));

        public Context<C> SelectMany<B, C>(Func<A, Context<B>> bind, Func<A, B, C> project) =>
            new Context<C>(ctx => Op(ctx).Bind(a => bind(a.Value).Op(a.Context).Map(b => (project(a.Value, b.Value), b.Context))));

        public static Context<A> operator |(Context<A> left, CatchValue<A> right) =>
            new Context<A>(ctx => {
                               var res = left.Op(ctx);
                               return res.IsSucc
                                          ? res
                                          : right.Match((Error) res)
                                              ? (right.Value((Error) res), ctx)
                                              : res;
                           });

        public static Context<A> operator |(Context<A> left, Context<A> right) =>
            new Context<A>(ctx => {
                               var res = left.Op(ctx);
                               return res.IsSucc
                                          ? res
                                          : right.Op(ctx);
                           });

    }

    public static class ContextExtensions
    {
        public static Context<A> ToContext<A>(this Fin<A> ma) =>
            ma.Case switch
            {
                A val     => Context.Pure(val),
                Error err => Context.Fail<A>(err),
                _         => throw new NotSupportedException()
            };

        public static Context<A> ToContext<A>(this Option<A> ma, Error error) =>
            ma.Case switch
            {
                A val => Context.Pure(val),
                _     => Context.Fail<A>(error)
            };
        
        public static Context<Seq<A>> Sequence<A>(this Seq<A> ms) =>
            ms.Sequence(Context.Pure);
 
        public static Context<Seq<B>> Sequence<A, B>(this Seq<A> ms, Func<A, Context<B>> f) =>
            ms.IsEmpty        ? Context.Pure<Seq<B>>(Empty)
          : ms.Tail.IsEmpty   ? f(ms.Head).Map(Seq1)
          : new Context<Seq<B>>(ctx => {

                                    Seq<B> result = Empty;

                                    foreach (var m in ms)
                                    {
                                        var fx = f(m).Op(ctx);
                                        if (fx.IsFail) return FinFail<(Seq<B>, Context)>((Error) fx);
                                        result = result.Add(fx.ThrowIfFail().Value);
                                        ctx    = fx.ThrowIfFail().Context;
                                    }

                                    return (result, ctx);
                                });
    }
}