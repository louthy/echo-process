using LanguageExt.Parsec;

namespace Echo.ActorSys2.Configuration
{
    public record Loc(string file, Pos Begin, Pos End)
    {
        public static readonly Loc None = new Loc("", Pos.Zero, Pos.Zero);

        public override string ToString() =>
            $"{file} {Begin}";
    }
}