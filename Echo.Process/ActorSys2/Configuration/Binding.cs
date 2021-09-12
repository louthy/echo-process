using LanguageExt;

namespace Echo.ActorSys2.Configuration
{
    public abstract record Binding
    {
        public virtual Context<Binding> Eval  =>
            Context.Pure(this);
    }

    public record NameBind : Binding;
    public record TyVarBind : Binding;
    public record VarBind(Ty Type) : Binding;

    public record TmAbbBind(Term Term, Option<Ty> Type) : Binding
    {
        public override Context<Binding> Eval =>
            from t in Term.Eval
            select new TmAbbBind(t, Type) as Binding;
    }

    public record TyAbbBind(Ty Type) : Binding;
}