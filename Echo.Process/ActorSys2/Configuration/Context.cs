using System;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.Configuration
{
    public record Context(HashMap<string, Binding> TopBindings, HashMap<string, Binding> Bindings)
    {
        /// <summary>
        /// Context success
        /// </summary>
        public static Context<A> Pure<A>(A value) =>
            new Context<A>(_ => FinSucc(value));
        
        /// <summary>
        /// Context fail
        /// </summary>
        public static Context<A> Fail<A>(Error error) =>
            new Context<A>(_ => FinFail<A>(error));
        
        /// <summary>
        /// Create a local context and run `ma` in it
        /// </summary>
        public static Context<A> local<A>(Func<Context, Context> f, Context<A> ma) =>
            new Context<A>(ctx => ma.Op(f(ctx)));

        /// <summary>
        /// Get the context
        /// </summary>
        public static Context<Context> get =>
            new Context<Context>(FinSucc);

        /// <summary>
        /// Get binding
        /// </summary>
        public static Context<Binding> getBinding(Loc loc, string name) =>
            get.Bind(ctx => ctx.GetBinding(loc, name));

        /// <summary>
        /// Get type
        /// </summary>
        public static Context<Ty> getType(Loc loc, string name) =>
            from b in getBinding(loc, name)
            from t in b switch
                      {
                          VarBind (var ty)                                => Pure(ty),
                          TmAbbBind (_, var oty) when oty.Case is (Ty ty) => Pure(ty),
                          TmAbbBind (_, var oty) when oty.Case is null    => Fail<Ty>(ProcessError.NoTypeRecordedForVariable(loc, name)),
                          _                                               => Fail<Ty>(ProcessError.WrongTypeOfBindingForVariable(loc, name))
                      }
            select t;

        /// <summary>
        /// Is the binding a type-lambda
        /// </summary>
        public static Context<bool> isTyAbb(string name) =>
            getBinding(Loc.None, name).Map(b => b is TyAbbBind);

        /// <summary>
        /// Get the binding if it's a type-lambda
        /// </summary>
        public static Context<Ty> getTyAbb(string name) =>
            getBinding(Loc.None, name).Bind(b => b is TyAbbBind ab ? Context.Pure(ab.Type) :  Context.Fail<Ty>(ProcessError.NoRuleApplies));

        public static Context<Ty> computeTy(Ty ty) =>
            ty switch
            {
                TyVar (var n) => getTyAbb(n),
                _             => Context.Fail<Ty>(ProcessError.NoRuleApplies)
            };

        public static Context<Ty> simplifyTy(Ty ty) =>
           (from ty1 in computeTy(ty)
            from ty2 in simplifyTy(ty1)
            select ty2)
          | @catch(ProcessError.NoRuleApplies, ty);
        
        /// <summary>
        /// Get the binding
        /// </summary>
        public Fin<Binding> GetBinding(Loc loc, string name) =>
            (Bindings.Find(name) || TopBindings.Find(name)).ToFin(default) || ProcessError.UndefinedBinding(loc, name);
        
        /// <summary>
        /// Add a top level binding
        /// </summary>
        public Fin<Context> AddTop(Loc loc, string name, Binding b) =>
            TopBindings.ContainsKey(name)
                ? this with {TopBindings = TopBindings.Add(name, b)}
                : ProcessError.TopLevelBindingAlreadyExists(loc, name);

        /// <summary>
        /// Add a local binding
        /// </summary>
        public Context AddLocal(string name, Binding b) =>
            this with {Bindings = Bindings.Add(name, b)};

        /// <summary>
        /// Check if a name is bound
        /// </summary>
        public bool IsNameBound(string name) =>
            Bindings.ContainsKey(name) || TopBindings.ContainsKey(name);

        /// <summary>
        /// Find a unique name from an existing name
        /// </summary>
        public string PickFreshName(string name) =>
            IsNameBound(name)
                ? PickFreshName($"{name}'")
                : name;
    }

    public class Context<A>
    {
        internal readonly Func<Context, Fin<A>> Op;

        internal Context(Func<Context, Fin<A>> op) =>
            Op = op;

        public Context<B> Select<B>(Func<A, B> f) =>
            new Context<B>(ctx => Op(ctx).Map(f));

        public Context<B> Map<B>(Func<A, B> f) =>
            new Context<B>(ctx => Op(ctx).Map(f));

        public Context<B> SelectMany<B>(Func<A, Context<B>> f) =>
            new Context<B>(ctx => Op(ctx).Bind<B>(a => f(a).Op(ctx)));

        public Context<B> Bind<B>(Func<A, Context<B>> f) =>
            new Context<B>(ctx => Op(ctx).Bind<B>(a => f(a).Op(ctx)));

        public Context<B> Bind<B>(Func<A, Fin<B>> f) =>
            new Context<B>(ctx => Op(ctx).Bind<B>(f));

        public Context<C> SelectMany<B, C>(Func<A, Context<B>> bind, Func<A, B, C> project) =>
            new Context<C>(ctx => Op(ctx).Bind(a => bind(a).Op(ctx).Map(b => project(a, b))));

        public static Context<A> operator |(Context<A> left, CatchValue<A> right) =>
            new Context<A>(ctx => {
                               var res = left.Op(ctx);
                               return res.IsSucc
                                          ? res
                                          : right.Match((Error) res)
                                              ? right.Value((Error) res)
                                              : res;
                           });

    }

    public static class ContextExtensions
    {
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
                                        if (fx.IsFail) return FinFail<Seq<B>>((Error) fx);
                                        result = result.Add((B) fx);
                                    }

                                    return result;
                                });
    }
}