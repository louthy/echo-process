using Echo.Traits;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2
{
    /// <summary>
    /// Internal state of the actor
    /// </summary>
    internal record ActorState<RT>(
        ProcessId Self,
        ProcessId Parent,
        HashMap<ProcessName, Actor<RT>> Children,
        State<StrategyContext, Unit> Strategy)
        where RT : struct, HasEcho<RT>
    {
        public static readonly ActorState<RT> None = new ActorState<RT>(ProcessId.None, ProcessId.None, Empty, Echo.Strategy.Identity);
    }
}