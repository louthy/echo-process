using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.Configuration
{
    public record Parameter(Loc Location, string Name, Ty Type);

    public record Prototype(Seq<Parameter> Parameters, Option<Ty> ReturnType)
    {
        public static readonly Prototype Default = new(Empty, None);

        public Seq<TyVar> GetVars() =>
            Parameters.Bind(static p => p.Type.GetVars()).Distinct();
    }
}