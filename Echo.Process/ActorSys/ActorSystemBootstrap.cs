using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using static LanguageExt.Prelude;
using static Echo.Process;
using System.Threading;
using System.Threading.Tasks;
using Echo.Config;
using Echo.Session;
using LanguageExt.UnitsOfMeasure;
using LanguageExt;

namespace Echo
{
    /// <summary>
    /// Represents the state of the whole actor system.  Mostly it holds the store of
    /// processes (actors) and their inboxes.
    /// </summary>
    internal class ActorSystemBootstrap
    {
        public readonly Option<ICluster> Cluster;
        public readonly IActorInbox RootInbox;
        public readonly IActor RootProcess;
        public readonly ActorSystemConfig Config;
        public readonly ProcessSystemConfig Settings;
        public readonly ProcessName RootProcessName;
        public readonly IActorSystem System;
        public readonly SessionSync Sync;

        ActorItem root;
        ActorItem user;
        ActorItem system;
        ActorItem deadLetters;
        ActorItem errors;
        ActorItem scheduler;
        ActorItem inboxShutdown;
        ActorItem sessionMonitor;
        ActorItem ask;
        ActorItem js;
        ActorItem reply;
        ActorItem monitor;

        public ActorSystemBootstrap(IActorSystem system, Option<ICluster> cluster, ProcessId rootId, IActor rootProcess, IActorInbox rootInbox, ProcessName rootProcessName, ActorSystemConfig config, ProcessSystemConfig settings, SessionSync sync)
        {
            System = system;
            Sync = sync;

            var parent = new ActorItem(new NullProcess(system.Name), new NullInbox(), ProcessFlags.Default);

            rootProcess = new Actor<ActorSystemBootstrap, Unit>(
                cluster,
                parent,
                rootProcessName,
                SystemInbox,
                _ => this.AsValueTask(),
                ActorSystem.NoShutdownAsync<ActorSystemBootstrap>(),
                null,
                Process.DefaultStrategy,
                ProcessFlags.Default,
                settings, 
                system
            );

            root = new ActorItem(rootProcess, rootInbox, rootProcess.Flags);
            Config = config;
            Settings = settings;
            Cluster = cluster;
            RootProcess = rootProcess;
            RootInbox = rootInbox;
            RootProcessName = rootProcessName;
        }

        public ValueTask<ActorSystemBootstrap> SystemInbox(ActorSystemBootstrap state, Unit msg)
        {
            return state.AsValueTask();
        }

        private Option<ActorItem> GetItem(ProcessId pid) =>
            pid.IsValid
                ? pid.Head() == RootProcess.Id
                    ? GetItem(pid.Tail(), root)
                    : failwith<Option<ActorItem>>("Can't get non-local items from within ActorSystemState")
                : None;

        private Option<ActorItem> GetItem(ProcessId pid, ActorItem current)
        {
            if (pid == ProcessId.Top)
            {
                return current;
            }
            else
            {
                var child = pid.Head().Name.Value;
                if (current.Actor.Children.ContainsKey(child))
                {
                    var process = current.Actor.Children[child];
                    return GetItem(pid.Tail(), process);
                }
                else
                {
                    return None;
                }
            }
        }

        private ProcessName NodeName =>
            Cluster.Map(c => c.NodeName).IfNone("user");

        public ActorSystemBootstrap Startup()
        {
            logInfo("Process system starting up");

            // Top tier
            system          = ActorCreate<object>(root, Config.SystemProcessName, publish, null, ProcessFlags.Default);
            user            = ActorCreate<object>(root, Config.UserProcessName, publish, null, ProcessFlags.Default);
            js              = ActorCreate<ProcessId, RelayMsg>(root, "js", RelayActor.Inbox, () => System.User["process-hub-js"], null, ProcessFlags.Default);

            // Second tier
            sessionMonitor  = ActorCreate<(SessionSync, Time), Unit>(system, Config.Sessions, SessionMonitor.Inbox, () => SessionMonitor.Setup(Sync, Settings.SessionTimeoutCheckFrequency), null, ProcessFlags.Default);
            deadLetters     = ActorCreate<DeadLetter>(system, Config.DeadLettersProcessName, publish, null, ProcessFlags.Default);
            errors          = ActorCreate<Exception>(system, Config.ErrorsProcessName, publish, null, ProcessFlags.Default);
            monitor         = ActorCreate<ClusterMonitor.State, ClusterMonitor.Msg>(system, Config.MonitorProcessName, ClusterMonitor.Inbox, () => ClusterMonitor.Setup(System), null, ProcessFlags.Default);

            Cluster.Iter(c =>
            {
                scheduler = ActorCreate<Scheduler.State, Scheduler.Msg>(system, Config.SchedulerName, Scheduler.Inbox, () => Scheduler.State.Empty, null, ProcessFlags.ListenRemoteAndLocal);
            });

            inboxShutdown   = ActorCreate<IActorInbox>(system, Config.InboxShutdownProcessName, inbox => inbox.Shutdown(), null, ProcessFlags.Default, 100000);

            reply = ask     = ActorCreate<(long, Dictionary<long, AskActorReq>), object>(system, Config.AskProcessName, AskActor.Inbox, AskActor.Setup, null, ProcessFlags.ListenRemoteAndLocal);

            logInfo("Process system startup complete");

            return this;
        }

        public Unit Shutdown()
        {
            logInfo("Process system shutting down");

            user?.Actor?.Shutdown(true);
            user = ActorCreate<object>(root, Config.UserProcessName, publish, null, ProcessFlags.Default);

            if (ActorContext.Request.CurrentRequest != null && ActorContext.Request.CurrentRequest.RequestId != -1)
            {
                tell(ActorContext.Request.CurrentRequest.ReplyTo, new ActorResponse(unit, ActorContext.Request.CurrentRequest.ReplyTo, root.Actor.Id, ActorContext.Request.CurrentRequest.RequestId, unit.GetType().AssemblyQualifiedName), System.Root);
            }

            logInfo("Process system shutdown complete");

            return unit;
        }

        public ActorItem ActorCreate<T>(ActorItem parent, ProcessName name, Func<T, Unit> actorFn, Func<Unit, ProcessId, Unit> termFn, ProcessFlags flags, int maxMailboxSize = -1) =>
            ActorCreateAsync<Unit, T>(parent, name, (s, t) => actorFn(t).AsValueTask(), static () => unit.AsValueTask(), (_, p) => termFn(unit, p).AsValueTask(), flags, maxMailboxSize);

        public ActorItem ActorCreate<T>(ActorItem parent, ProcessName name, Action<T> actorFn, Func<Unit, ProcessId, Unit> termFn, ProcessFlags flags, int maxMailboxSize = -1) =>
            ActorCreateAsync<Unit, T>(parent, name, (s, t) => { actorFn(t); return unit.AsValueTask(); }, static () => unit.AsValueTask(), (_, p) => termFn(unit, p).AsValueTask(), flags, maxMailboxSize);

        public ActorItem ActorCreate<S, T>(ActorItem parent, ProcessName name, Func<S, T, S> actorFn, Func<S> setupFn, Func<S, ProcessId, S> termFn, ProcessFlags flags, int maxMailboxSize = -1) =>
            ActorCreateAsync<S, T>(parent, name, (s, m) => actorFn(s,m).AsValueTask(), () => setupFn().AsValueTask(), (s, p) => termFn(s, p).AsValueTask(), flags, maxMailboxSize);
        
        public ActorItem ActorCreateAsync<T>(ActorItem parent, ProcessName name, Func<T, ValueTask<Unit>> actorFn, Func<Unit, ProcessId, ValueTask<Unit>> termFn, ProcessFlags flags, int maxMailboxSize = -1) =>
            ActorCreateAsync<Unit, T>(parent, name, async (s, t) => { await actorFn(t).ConfigureAwait(false); return unit; }, static () => unit.AsValueTask(), termFn, flags, maxMailboxSize);

        public ActorItem ActorCreateAsync<T>(ActorItem parent, ProcessName name, Action<T> actorFn, Func<Unit, ProcessId, ValueTask<Unit>> termFn, ProcessFlags flags, int maxMailboxSize = -1) =>
            ActorCreateAsync<Unit, T>(parent, name, (s, t) => { actorFn(t); return unit.AsValueTask(); }, static () => unit.AsValueTask(), termFn, flags, maxMailboxSize);

        public ActorItem ActorCreateAsync<S, T>(ActorItem parent, ProcessName name, Func<S, T, ValueTask<S>> actorFn, Func<ValueTask<S>> setupFn, Func<S, ProcessId, ValueTask<S>> termFn, ProcessFlags flags, int maxMailboxSize = -1)
        {
            if (ProcessDoesNotExist(nameof(ActorCreate), parent.Actor.Id)) return null;

            var actor = new Actor<S, T>(Cluster, parent, name, actorFn, _ => setupFn(), ActorSystem.NoShutdownAsync<S>(), termFn, Process.DefaultStrategy, flags, Settings, System);

            IActorInbox inbox = null;
            if ((actor.Flags & ProcessFlags.ListenRemoteAndLocal) == ProcessFlags.ListenRemoteAndLocal && Cluster.IsSome)
            {
                inbox = new ActorInboxDual<S, T>();
            }
            else if ((actor.Flags & ProcessFlags.PersistInbox) == ProcessFlags.PersistInbox && Cluster.IsSome)
            {
                inbox = new ActorInboxRemote<S, T>();
            }
            else
            {
                inbox = new ActorInboxLocal<S, T>();
            }

            var item = new ActorItem(actor, inbox, actor.Flags);
            try
            {
                parent.Actor.LinkChild(item);
                inbox.Startup(actor, System, actor.Parent, Cluster, 
                    maxMailboxSize == -1 
                        ? Settings.GetProcessMailboxSize(actor.Id)
                        : maxMailboxSize
                    );
            }
            catch (Exception e)
            {
                item.Actor.Shutdown(false);
                logSysErr(new ProcessException($"Process failed starting up: {e.Message}", actor.Id.Path, actor.Parent.Actor.Id.Path, e));
            }

            return item;
        }

        private bool ProcessDoesNotExist(string func, ProcessId pid)
        {
            if (GetItem(pid))
            {
                return false;
            }
            else
            {
                logErr($"{func}: process doesn't exist: {pid}");
                return true;
            }
        }
    }
}
