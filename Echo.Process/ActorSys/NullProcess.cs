using System;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo
{
    class NullProcess<RT> : IActor<RT> where RT : struct, HasEcho<RT>
    {
        public readonly SystemName System;
        
        public NullProcess(SystemName system) =>
             System = system;
        
        public ProcessId Id => 
            ProcessId.Top.SetSystem(System);
        
        public ProcessName Name => 
            "$";
        
        public ActorItem Parent => 
            new ActorItem(new NullProcess<RT>(System), new NullInbox(), ProcessFlags.Default);
        
        public ProcessFlags Flags => 
            ProcessFlags.Default;

        public State<StrategyContext, Unit> Strategy =>
            Process.DefaultStrategy;

        public HashMap<string, ActorItem> Children =>
            Empty;
        
        public Eff<Unit> LinkChild(ActorItem pid) =>
            SuccessEff(unit);

        public Eff<Unit> UnlinkChild(ProcessId pid) =>
            SuccessEff(unit);

        public Eff<Unit> Publish(object message) =>
            SuccessEff(unit);

        public IObservable<object> PublishStream =>
            null;

        public IObservable<object> StateStream =>
            null;

        public Eff<Unit> AddSubscription(ProcessId pid, IDisposable sub) =>
            SuccessEff(unit);

        public Eff<Unit> RemoveSubscription(ProcessId pid) =>
            SuccessEff(unit);

        public Eff<Unit> RemoveAllSubscriptions() =>
            SuccessEff(unit);

        public Eff<Unit> ResetStrategyState() =>
            SuccessEff(unit);

        public Eff<Unit> SetStrategyState(StrategyState state) =>
            SuccessEff(unit);

        public StrategyState StrategyState =>
            StrategyState.Empty;
        
        public IActor<LRT> WithRuntime<LRT>() where LRT : struct, HasEcho<LRT> =>
            (IActor<LRT>) ((object) this);

        public Aff<RT, Unit> Restart(bool unpauseAfterRestart) =>
            SuccessEff(unit);

        public Aff<RT, InboxDirective> Startup =>
            SuccessEff(InboxDirective.Default);
        
        public Aff<RT, Unit> Shutdown(bool maintainState) =>
            SuccessEff(unit);

        public Eff<RT, Unit> AddWatcher(ProcessId pid) =>
            SuccessEff(unit);

        public Eff<RT, Unit> RemoveWatcher(ProcessId pid) =>
            SuccessEff(unit);

        public Aff<RT, InboxDirective> ProcessTell =>
            SuccessEff(InboxDirective.Default);
        
        public Aff<RT, InboxDirective> ProcessAsk =>
            SuccessEff(InboxDirective.Default);
        
        public Aff<RT, InboxDirective> ProcessTerminated(ProcessId id) =>
            SuccessEff(InboxDirective.Default);

        public Aff<RT, Unit> DispatchWatch(ProcessId pid) =>
            SuccessEff(unit);

        public Aff<RT, Unit> DispatchUnWatch(ProcessId pid) =>
            SuccessEff(unit);
    }    
}