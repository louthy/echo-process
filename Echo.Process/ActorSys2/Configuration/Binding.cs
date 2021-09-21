using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.Configuration
{
    public abstract record Binding
    {
        public virtual Context<Binding> Eval  =>
            Context.Pure(this);

        public virtual Option<TyLamBind> AsTyLam =>
            None;

        public virtual Fin<Kind> GetKind(Loc location, string name) =>
            FinFail<Kind>(ProcessError.NonVariableBinding(location, name));
    }

    /// <summary>
    /// Name binding, no type, no term
    /// </summary>
    public record NameBind : Binding
    {
        public static readonly Binding Default = new NameBind();
    }

    /// <summary>
    /// Type variable kind binding
    /// </summary>
    public record TyVarBind(Kind Kind) : Binding
    {
        public override Fin<Kind> GetKind(Loc location, string name) =>
            Kind;
    }

    /// <summary>
    /// Type lambda abstraction binding
    /// </summary>
    public record TyLamBind(Ty Type, Option<Kind> Kind) : Binding
    {
        public override Option<TyLamBind> AsTyLam =>
            Some(this);

        public override Fin<Kind> GetKind(Loc location, string name) =>
            Kind.ToFin(ProcessError.NoKindRecordedForVariable(location, name));
    }

    /// <summary>
    /// Type variable binding
    /// </summary>
    public record VarBind(Ty Type) : Binding;

    /// <summary>
    /// Term and type binding
    /// </summary>
    public record TmAbbBind(Term Term, Option<Ty> Type) : Binding
    {
        public override Context<Binding> Eval =>
            from t in Term.Eval
            select new TmAbbBind(t, Type) as Binding;
    }

}