using Echo.Traits;
using LanguageExt;

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
        where RT : struct, HasEcho<RT>;
}