using LanguageExt;
using LanguageExt.UnitsOfMeasure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static LanguageExt.Prelude;

namespace Echo
{
    class NullProcess : IActor
    {
        public readonly SystemName System;

        public NullProcess(SystemName system)
        {
            System = system;
        }

        public HashMap<string, ActorItem> Children => HashMap<string, ActorItem>();
        public ProcessId Id => ProcessId.Top.SetSystem(System);
        public ProcessFlags Flags => ProcessFlags.Default;
        public ProcessName Name => "$";
        public ActorItem Parent => new ActorItem(new NullProcess(System), new NullInbox(), ProcessFlags.Default);
        public State<StrategyContext, Unit> Strategy => Process.DefaultStrategy;
        public ValueTask<Unit> Restart(bool unpauseAfterRestart) => unit.AsValueTask();
        public ValueTask<InboxDirective> Startup() => InboxDirective.Default.AsValueTask();
        public ValueTask<Unit> Shutdown(bool maintainState) => unit.AsValueTask();
        public Unit LinkChild(ActorItem item) => unit;
        public Unit UnlinkChild(ProcessId item) => unit;
        public Unit AddWatcher(ProcessId item) => unit;
        public Unit RemoveWatcher(ProcessId item) => unit;
        public Unit DispatchWatch(ProcessId item) => unit;
        public Unit DispatchUnWatch(ProcessId item) => unit;
        public bool Pause() => false;
        public bool UnPause() => false;
        public bool IsPaused => false;
        public Unit Publish(object message) => unit;
        public IObservable<object> PublishStream => null;
        public IObservable<object> StateStream => null;

        public CancellationTokenSource CancellationTokenSource => new CancellationTokenSource();

        public ValueTask<InboxDirective> ProcessTerminated(ProcessId pid) => InboxDirective.Default.AsValueTask();
        public ValueTask<InboxDirective> ProcessMessage(object message) => InboxDirective.Default.AsValueTask();
        public ValueTask<InboxDirective> ProcessAsk(ActorRequest request) => InboxDirective.Default.AsValueTask();
        public Unit AddSubscription(ProcessId pid, IDisposable sub) => Unit.Default;
        public Unit RemoveSubscription(ProcessId pid) => Unit.Default;
        public int GetNextRoundRobinIndex() => 0;
        public ValueTask<Unit> ProcessResponse(ActorResponse response) => unit.AsValueTask();

        public void Dispose()
        {
        }

    }
}
