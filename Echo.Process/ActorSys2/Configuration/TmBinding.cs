using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.Configuration
{
    public abstract record TmBinding
    {
        public virtual Context<TmBinding> Eval =>
            Context.Pure(this);
    }

    /// <summary>
    /// Name binding, no type, no term
    /// </summary>
    public record TmNameBind : TmBinding
    {
        public static readonly TmBinding Default = new TmNameBind();
    }

    /// <summary>
    /// Type variable binding
    /// </summary>
    public record TmVarBind(Ty Type) : TmBinding;

    /// <summary>
    /// Term and type binding
    /// </summary>
    public record TmAbbBind(Term Term, Option<Ty> Type) : TmBinding
    {
        public override Context<TmBinding> Eval =>
            from t in Term.Eval
            select (TmBinding) new TmAbbBind(t, Type);
    }
}