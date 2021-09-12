using Echo.Config;
using LanguageExt;
using LanguageExt.UnitsOfMeasure;

namespace Echo.ActorSys2.Configuration
{
    public abstract record Const(Loc Location);
    public record IntConst(long Value, Loc Location) : Const(Location);
    public record FloatConst(double Value, Loc Location) : Const(Location);
    public record StringConst(string Value, Loc Location) : Const(Location);
    public record ProcessIdConst(ProcessId Value, Loc Location) : Const(Location);
    public record ProcessNameConst(ProcessName Value, Loc Location) : Const(Location);
    public record ProcessFlagConst(ProcessFlags Value, Loc Location) : Const(Location);
    public record TimeConst(Time Value, Loc Location) : Const(Location);
    public record MessageDirectiveConst(MessageDirective Value, Loc Location) : Const(Location);
    public record InboxDirectiveConst(InboxDirective Value, Loc Location) : Const(Location);
    public record DispatcherConst(string Value, Loc Location) : Const(Location);
    public record TrueConst(Loc Location) : Const(Location);
    public record FalseConst(Loc Location) : Const(Location);
}