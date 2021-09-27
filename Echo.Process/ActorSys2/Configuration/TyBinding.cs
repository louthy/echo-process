using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.Configuration
{
    public abstract record TyBinding
    {
        public virtual Context<TyBinding> Eval  =>
            Context.Pure(this);

        public virtual Option<TyLamBind> AsTyLam =>
            None;

        public virtual Fin<Kind> GetKind(Loc location, string name) =>
            FinFail<Kind>(ProcessError.NonVariableBinding(location, name));
    }

    /// <summary>
    /// Name binding, no type
    /// </summary>
    public record TyNameBind : TyBinding
    {
        public static readonly TyBinding Default = new TyNameBind();
    }

    /// <summary>
    /// Type variable kind binding
    /// </summary>
    public record TyVarBind(Kind Kind) : TyBinding
    {
        public override Fin<Kind> GetKind(Loc location, string name) =>
            Kind;
    }

    /// <summary>
    /// Type lambda abstraction binding
    /// </summary>
    public record TyLamBind(Ty Type, Option<Kind> Kind) : TyBinding
    {
        public override Option<TyLamBind> AsTyLam =>
            Some(this);

        public override Fin<Kind> GetKind(Loc location, string name) =>
            Kind.ToFin(ProcessError.NoKindRecordedForVariable(location, name));
    }
}