using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.Configuration
{
    public class TypeChecker
    {
        public static Context<Unit> Decls(Seq<Decl> decls) =>
            decls.Sequence(d => d.TypeCheck()).Map(static _ => unit);
    }
}