using System;
using System.Linq;
using LanguageExt;
using Echo.Config;
using Echo.Session;
using Echo.ActorSys;
using System.Threading;
using LanguageExt.Common;
using System.Reactive.Linq;
using System.Threading.Tasks;
using LanguageExt.Interfaces;
using static LanguageExt.Prelude;
using G = System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Reactive.Concurrency;
using System.Security.Cryptography;

namespace Echo
{
    class WatchState
    {
        public static readonly WatchState Empty = new WatchState(default, default);
         
        public readonly HashMap<ProcessId, HashSet<ProcessId>> Watchers;
        public readonly HashMap<ProcessId, HashSet<ProcessId>> Watchings;

        WatchState(
            HashMap<ProcessId, HashSet<ProcessId>> watchers,
            HashMap<ProcessId, HashSet<ProcessId>> watchings) =>
            (Watchers, Watchings) = (watchers, watchings);

        public WatchState AddWatcher(ProcessId watching, ProcessId watcher) =>
            new WatchState(Watchers.AddOrUpdate(watching, Some: ws => ws.AddOrUpdate(watcher), None: () => Prelude.HashSet(watcher)), Watchings);

        public WatchState AddWatching(ProcessId watcher, ProcessId watching) =>
            new WatchState(Watchers, Watchings.AddOrUpdate(watcher, Some: ws => ws.AddOrUpdate(watching), None: () => Prelude.HashSet(watching)));

        public WatchState RemoveWatcher(ProcessId watching, ProcessId watcher)
        {
            var idwatchers = Watchers.Find(watching)
                                     .Match(Some: ws => ws.Remove(watcher), None: HashSet<ProcessId>());
            
            var watchers = idwatchers.IsEmpty
                               ? Watchers.Remove(watching)
                               : Watchers.SetItem(watching, idwatchers);
            
            return new WatchState(watchers, Watchings);
        }

        public WatchState RemoveWatcher(ProcessId watching) =>
            new WatchState(Watchers.Remove(watching), Watchings);

        public WatchState RemoveWatching(ProcessId watcher, ProcessId watching)
        {
            var idwatchings = Watchings.Find(watcher)
                                       .Match(Some: ws => ws.Remove(watching), None: HashSet<ProcessId>());
            
            var watchings = idwatchings.IsEmpty
                               ? Watchings.Remove(watcher)
                               : Watchings.SetItem(watcher, idwatchings);
            
            return new WatchState(Watchers, watchings);
        }
        
        public WatchState RemoveWatching(ProcessId watcher) =>
            new WatchState(Watchers, Watchings.Remove(watcher));
    }

    /// <summary>
    /// Cache of registered names and pids
    /// </summary>
    class RegisteredState
    {
        public static readonly RegisteredState Empty = new RegisteredState(default, default);
         
        public readonly HashMap<ProcessName, LanguageExt.HashSet<ProcessId>> RegisteredProcessNames;
        public readonly HashMap<ProcessId, LanguageExt.HashSet<ProcessName>> RegisteredProcessIds;

        RegisteredState(
            HashMap<ProcessName, LanguageExt.HashSet<ProcessId>> registeredProcessNames,
            HashMap<ProcessId, LanguageExt.HashSet<ProcessName>> registeredProcessIds) =>
            (RegisteredProcessNames, RegisteredProcessIds) = (registeredProcessNames, registeredProcessIds);

        public RegisteredState RemoveLocalRegisteredByName(ProcessName name)
        {
            var pids = RegisteredProcessNames.Find(name).IfNone(HashSet<ProcessId>());
            var registeredProcessIds = pids.Fold(RegisteredProcessIds, (rpids, pid) => rpids.SetItem(pid, rpids[pid].Remove(name)));
            return new RegisteredState(RegisteredProcessNames.Remove(name), registeredProcessIds);
        }
        
        public RegisteredState RemoveLocalRegisteredById(ProcessId pid)
        {
            var names = RegisteredProcessIds.Find(pid).IfNone(HashSet<ProcessName>());
            var registeredProcessNames = names.Fold(RegisteredProcessNames, (rpids, name) => rpids.SetItem(name, rpids[name].Remove(pid)));
            return new RegisteredState(registeredProcessNames, RegisteredProcessIds.Remove(pid));
        }  
        
        public RegisteredState AddLocalRegistered(ProcessName name, ProcessId pid, SystemName systemName)
        {
            var registeredProcessNames = RegisteredProcessNames.AddOrUpdate(
                    name,
                    Some: set => set.AddOrUpdate(pid.SetSystem(systemName)),
                    None: ()  => HashSet(pid)
                );
            
            var registeredProcessIds = RegisteredProcessIds.AddOrUpdate(
                    pid.SetSystem(systemName),
                    Some: set => set.AddOrUpdate(name),
                    None: () => HashSet(name)
                );

            return new RegisteredState(registeredProcessNames, registeredProcessIds);
        }
    }

    class ActorSystem
    {
        public readonly Atom<ClusterMonitor.State> ClusterState;
        public readonly Atom<WatchState> Watch;
        public readonly Atom<RegisteredState> Registered;
        public readonly Atom<ProcessSystemConfig> Settings;
        public readonly Atom<AppProfile> AppProfile;
        public readonly IDisposable StartupSubscription;
        public readonly ActorItem RootItem;
        public readonly bool Cluster;
        public readonly long StartupTimestamp;
        public readonly SessionManager SessionManager;
        public readonly Ping Ping;
        public readonly SystemName SystemName;
        
        // TODO: Below not yet constructed
        
        public ILocalActorInbox LocalRoot => (ILocalActorInbox)RootItem.Inbox;
        public IActorInbox RootInbox => RootItem.Inbox;

        public readonly ProcessId Root;
        public readonly ProcessId User;
        public readonly ProcessId Errors;
        public readonly ProcessId Scheduler;
        public readonly ProcessId DeadLetters;
        public readonly ProcessId Disp;
        public readonly ProcessId RootJS;
        public readonly ProcessId System;
        private readonly ProcessName NodeName;
        internal readonly ProcessId AskId;

        public SystemName Name => SystemName;
        public SessionManager Sessions => SessionManager;

        public ActorSystem(
            Atom<ClusterMonitor.State> clusterState, 
            Atom<WatchState> watch, 
            Atom<RegisteredState> registered, 
            Atom<ProcessSystemConfig> settings, 
            Atom<AppProfile> appProfile, 
            IDisposable startupSubscription, 
            ActorItem rootItem, 
            bool cluster,
            long startupTimestamp, 
            SessionManager sessionManager, 
            Ping ping, 
            SystemName systemName,
            ProcessName nodeName)
        {
            Root        = ProcessId.Top[systemName.Value];
            RootJS      = Root["js"];
            User        = Root[ActorSystemConfig.Default.UserProcessName];
            System      = Root[ActorSystemConfig.Default.SystemProcessName];
            DeadLetters = System[ActorSystemConfig.Default.DeadLettersProcessName];
            Errors      = System[ActorSystemConfig.Default.ErrorsProcessName];
            Scheduler   = System[ActorSystemConfig.Default.SchedulerName];
            AskId       = System[ActorSystemConfig.Default.AskProcessName];
            NodeName    = nodeName; //cluster.Map(c => c.NodeName).IfNone("user");
            
            ClusterState = clusterState;
            Watch = watch;
            Registered = registered;
            Settings = settings;
            AppProfile = appProfile;
            StartupSubscription = startupSubscription;
            RootItem = rootItem;
            Cluster = cluster;
            StartupTimestamp = startupTimestamp;
            SessionManager = sessionManager;
            Ping = ping;
            SystemName = systemName;
        }

        public ActorSystem With(ActorItem RootItem = null,
            SessionManager SessionManager = null,
            Ping Ping = null) =>
            new ActorSystem(
                ClusterState,
                Watch,
                Registered,
                Settings,
                AppProfile,
                StartupSubscription,
                RootItem ?? this.RootItem,
                this.Cluster,
                StartupTimestamp,
                SessionManager ?? this.SessionManager,
                Ping ?? this.Ping,
                SystemName);

        public Eff<Unit> UpdateSettings(ProcessSystemConfig settings, AppProfile profile) =>
            from _1 in Settings.SwapEff(_ => SuccessEff(settings))
            from _2 in AppProfile.SwapEff(_ => SuccessEff(profile))
            select unit;

        public Eff<Unit> ApplyUpdate(SettingOverride update) =>
            from _ in swapEff(Settings, s => SuccessEff(s.With(SettingOverrides: s.SettingOverrides.ApplyUpdate(update))))
            select unit;

        public Eff<Unit> ApplyUpdates(SettingOverrideUpdates updates) =>
            from _ in swapEff(Settings, s => SuccessEff(s.With(SettingOverrides: s.SettingOverrides.ApplyUpdates(updates))))
            select unit;

        public Eff<Unit> ApplyUpdates<A>(Option<SettingOverrideUpdates> ma) =>
            ma.Match(
                Some: ApplyUpdates,
                None: () => unitEff);

        public Eff<RegisteredState> RemoveLocalRegisteredByName(ProcessName name) =>
            swapEff(Registered, s => SuccessEff(s.RemoveLocalRegisteredByName(name)));

        public Eff<RegisteredState> RemoveLocalRegisteredById(ProcessId pid) =>
            swapEff(Registered, s => SuccessEff(s.RemoveLocalRegisteredById(pid)));

        public Eff<RegisteredState> AddLocalRegistered(ProcessName name, ProcessId pid) =>
            swapEff(Registered, s => SuccessEff(s.AddLocalRegistered(name, pid, SystemName)));

        public Eff<WatchState> ProcessTerminated(ProcessId terminating) =>
            Watch.SwapEff(ws => SuccessEff(ws.RemoveWatcher(terminating).RemoveWatching(terminating)));

        public Eff<Unit> AddWatcher(ProcessId watcher, ProcessId watching) =>
            Watch.SwapEff(ws => SuccessEff(ws.AddWatcher(watching, watcher).AddWatching(watcher, watching)))
                 .Map(_ => unit);
        
        public Eff<Unit> RemoveWatcher(ProcessId watcher, ProcessId watching) =>
            Watch.SwapEff(ws => SuccessEff(ws.RemoveWatcher(watching, watcher).RemoveWatching(watcher, watching)))
                 .Map(_ => unit);

        public Aff<RT, Unit> Dispose<RT>() where RT : struct, HasEcho<RT> =>
            from _1 in Ping.Dispose()
            from _2 in Eff(() => { StartupSubscription.Dispose(); return unit; })
            from _3 in RootItem.Actor.Children.Values
                               .OrderByDescending(c => c.Actor.Id == User) // shutdown "user" first
                               .Map(c => ActorSystemAff<RT>.withContext(c, ActorAff<RT>.shutdownProcess(true)))
                               .SequenceSerial()
            from _4 in Cluster ? Echo.Cluster.disconnect<RT>() : SuccessEff(true)
            select unit;
    }

    static class ActorSystemAff<RT> 
        where RT : struct, HasEcho<RT>
    {
        const string ClusterOnlineKey = "cluster-node-online";

        public static Func<S, Aff<RT, Unit>> NoShutdown<S>() => (S s) => SuccessAff(unit);

        public static Aff<RT, ActorSystem> Init<RT>(SystemName systemName, bool cluster, AppProfile appProfile, ProcessSystemConfig settings) 
            where RT : struct, HasCancel<RT>
        {
            var name = GetRootProcessName(cluster);
            if (name.Value == "root" && cluster.IsSome) throw new ArgumentException("Cluster node name cannot be 'root', it's reserved for local use only.");
            if (name.Value == "disp" && cluster.IsSome) throw new ArgumentException("Cluster node name cannot be 'disp', it's reserved for internal use.");
            if (name.Value == "js") throw new ArgumentException("Node name cannot be 'js', it's reserved for ProcessJS.");

            SystemName = systemName;
            this.appProfile = appProfile;
            this.settings = Atom(settings);
            this.cluster = cluster;
            Ping = new Ping(this);
            startupTimestamp = DateTime.UtcNow.Ticks;
            sessionManager = new SessionManager(cluster, SystemName, appProfile.NodeName, VectorConflictStrategy.Branch);
            watchers = HashMap<ProcessId, Set<ProcessId>>();
            watchings = HashMap<ProcessId, Set<ProcessId>>();

            startupSubscription = NotifyCluster(cluster, startupTimestamp);

            Dispatch.init();
            Role.init(cluster.Map(r => r.Role).IfNone("local"));
            Reg.init();

            var root = ProcessId.Top.Child(GetRootProcessName(cluster));
            var rootInbox = new ActorInboxLocal<ActorSystemBootstrap, Unit>();
            var parent = new ActorItem(new NullProcess(SystemName), new NullInbox(), ProcessFlags.Default);

            var state = new ActorSystemBootstrap(
                this,
                cluster,
                root, null,
                rootInbox,
                cluster.Map(x => x.NodeName).IfNone(ActorSystemConfig.Default.RootProcessName),
                ActorSystemConfig.Default,
                Settings,
                sessionManager.Sync
                );

            var rootProcess = state.RootProcess;
            state.Startup();
            rootItem = new ActorItem(rootProcess, rootInbox, ProcessFlags.Default);

            Root            = rootItem.Actor.Id;
            RootJS          = Root["js"];
            System          = Root[ActorSystemConfig.Default.SystemProcessName];
            User            = Root[ActorSystemConfig.Default.UserProcessName];
            Errors          = System[ActorSystemConfig.Default.ErrorsProcessName];
            DeadLetters     = System[ActorSystemConfig.Default.DeadLettersProcessName];
            NodeName        = cluster.Map(c => c.NodeName).IfNone("user");
            AskId           = System[ActorSystemConfig.Default.AskProcessName];
            Disp            = ProcessId.Top["disp"].SetSystem(SystemName);
            Scheduler       = System[ActorSystemConfig.Default.SchedulerName];

            userContext = new ActorRequestContext(
                this,
                rootProcess.Children["user"],
                ProcessId.NoSender,
                rootItem,
                null,
                null,
                ProcessFlags.Default,
                null);
            rootInbox.Startup(rootProcess, parent, cluster, settings.GetProcessMailboxSize(rootProcess.Id));
        }

        public void Initialise()
        {
            ClusterWatch(cluster);
            SchedulerStart(cluster);
        }

        class ClusterOnline
        {
            public string Name;
            public long Timestamp;
        }

        /// <summary>
        /// Sends an notification to the cluster that we're coming online, also watches out for other services coming
        /// online with the same node-name.  If so, we shutdown.
        /// </summary>
        /// <param name="cluster"></param>
        /// <param name="system"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        public static Aff<RT, IDisposable> notifyCluster(bool cluster, SystemName system, long timestamp) =>
            cluster
                ? SuccessEff(Observable.Return(new ClusterOnline()).Take(1).Subscribe())
                : from _ in Cluster.publishToChannel<RT, ClusterOnline>(ClusterOnlineKey, new ClusterOnline { Name = system.Value, Timestamp = timestamp })
                  from s in Cluster.subscribeToChannel<RT, ClusterOnline>(ClusterOnlineKey)
                  from d in watchForNodesWithSameName(system, timestamp, s)  
                  select d;

        /// <summary>
        /// Protects against multiple nodes with the same name running
        /// </summary>
        static Eff<RT, IDisposable> watchForNodesWithSameName(SystemName system, long timestamp, IObservable<ClusterOnline> obs) =>
            from e in runtime<RT>()
            select obs.Where(node => node.Name == system.Value && node.Timestamp > timestamp)
                      .Take(1)
                      .SelectMany(async _ => await (from _1 in ProcessEff.logErr($"Another node has come online with the same node-name ({system}), shutting down")
                                                    from _2 in ProcessAff<RT>.shutdownSystem(system)
                                                    select unit).RunIO(e).ConfigureAwait(false))
                      .ObserveOn(TaskPoolScheduler.Default)
                      .Subscribe(_ => { });
        
        [Pure]
        public static Eff<RT, ClusterMonitor.State> ClusterState =>
            from sys in ActorContextAff<RT>.LocalSystem
            select sys.ClusterState.Value;

        [Pure]
        static Aff<RT, Unit> clusterWatch =>
            from sys in ActorContextAff<RT>.LocalSystem
            from res in sys.Cluster
                            ? from cfg in ActorContextAff<RT>.GlobalSettings
                              from mon in SuccessEff(sys.System[ActorSystemConfig.Default.MonitorProcessName])
                              from __1 in clusterNodeOnlineWatcher
                              from __2 in clusterNodeOfflineWatcher
                              from __3 in clusterStateWatcher
                              select unit
                            : unitEff
            select res;

        [Pure]
        static Aff<RT, IDisposable> clusterNodeOnlineWatcher =>
            from sys in ActorContextAff<RT>.LocalSystem
            from mon in SuccessEff(sys.System[ActorSystemConfig.Default.MonitorProcessName])
            from dis in observe<NodeOnline>(mon).Map(s => s.Subscribe(x => Process.logInfo("Online: " + x.Name)))
            select dis;
 
        [Pure]
        static Aff<RT, IDisposable> clusterNodeOfflineWatcher =>
            from sys in ActorContextAff<RT>.LocalSystem
            from mon in SuccessEff(sys.System[ActorSystemConfig.Default.MonitorProcessName])
            from env in runtime<RT>()
            from dis in observe<NodeOffline>(mon)
                            .Map(s => s.SelectMany(async x => await removeWatchingOfRemote(x.Name)
                                                                        .Map(_ => x)
                                                                        .RunIO(env)
                                                                        .ConfigureAwait(false))
                                       .Where(x => x.IsSucc)
                                       .Select(x => x.IfFail(_ => default(NodeOffline)))
                                       .Subscribe(x => Process.logInfo("Offline: " + x.Name)))
            select dis;

        [Pure]
        static Aff<RT, IDisposable> clusterStateWatcher =>
            from sys in ActorContextAff<RT>.LocalSystem
            from mon in SuccessEff(sys.System[ActorSystemConfig.Default.MonitorProcessName])
            from dis in observeState<ClusterMonitor.State>(mon).Map(s => s.Subscribe(x => sys.ClusterState.Swap(_ => x)))
            select dis;

        [Pure]
        static Aff<RT, Unit> schedulerStart =>
            from sys in ActorContextAff<RT>.LocalSystem
            from res in sys.Cluster
                            ? from cfg in ActorContextAff<RT>.GlobalSettings
                              from pid in SuccessEff(sys.System[cfg.SchedulerName])
                              from ___ in tell(pid,  Echo.Scheduler.Msg.Check, Schedule.Immediate, sys.User)
                              select unit
                            : unitEff
            select res;

        [Pure]
        static Aff<RT, Unit> removeWatchingOfRemote(ProcessName node) =>
            ActorContextAff<RT>.localSystem(new SystemName(node.Value),
                from lsys in ActorContextAff<RT>.LocalSystem
                from root in SuccessEff(ProcessId.Top[node])
                from wats in SuccessEff(lsys.Watch.Value.Watchings.Keys.Filter(w => w.Take(1) == root))
                from ____ in wats.Map(remoteDispatchTerminate).SequenceParallel()
                select unit);

        [Pure]
        public static Aff<RT, Unit> remoteDispatchTerminate(ProcessId terminating) =>
            ActorContextAff<RT>.localSystem(terminating,
                from sys in ActorContextAff<RT>.LocalSystem
                from trm in SuccessEff(new TerminatedMessage(terminating))
                from ws  in sys.Watch.Value.Watchings.Find(terminating).Match(SuccessEff, SuccessEff(HashSet<ProcessId>()))
                from __1 in ws.Map(w => ProcessAff<RT>.catchAndLogErr(tellUserControl(w, trm), unitEff)).SequenceParallel()
                from __2 in sys.ProcessTerminated(terminating)
                select unit);

        [Pure]
        public static Eff<RT, Unit> addWatcher(ProcessId watcher, ProcessId watching) =>
            ActorContextAff<RT>.localSystem(watcher,
                from sys in ActorContextAff<RT>.LocalSystem 
                from __1 in sys.AddWatcher(watcher, watching)
                from __2 in ProcessEff.logInfo(watcher + " is watching " + watching)
                select unit); 
        
        [Pure]
        public static Eff<RT, Unit> removeWatcher(ProcessId watcher, ProcessId watching) =>
            ActorContextAff<RT>.localSystem(watcher,
                from sys in ActorContextAff<RT>.LocalSystem 
                from __1 in sys.RemoveWatcher(watcher, watching)
                from __2 in ProcessEff.logInfo(watcher + " is watching " + watching)
                select unit); 

        [Pure]
        public static Aff<RT, Unit> dispatchTerminate(ProcessId terminating) =>
            ActorContextAff<RT>.localSystem(terminating,
                from sys in ActorContextAff<RT>.LocalSystem
                from was in sys.Watch.Value.Watchers.Find(terminating).Match(
                                Some: SuccessEff,
                                None: FailEff<LanguageExt.HashSet<ProcessId>>(Error.New($"Watcher not found: {terminating}")))
                from trm in SuccessEff(new TerminatedMessage(terminating))
                from __1 in was.Map(w => ProcessAff<RT>.catchAndLogErr(tellUserControl(w, trm).Map(_ => unit), unitEff))
                               .SequenceParallel()
                from __2 in sys.Watch.Value.Watchings
                              .Find(terminating)
                              .Map(ws => ws.Map(w => from d in ActorSystemAff<RT>.getDispatcher(w)
                                                     from _ in d.UnWatch<RT>(terminating)
                                                     select unit)
                                            .SequenceParallel())
                              .Sequence()
                from __3 in sys.ProcessTerminated(terminating)
                select unit);

        [Pure]
        public static Aff<RT, Seq<A>> askMany<A>(Seq<ProcessId> pids, object message, ProcessId sender) =>
            pids.Map(pid => ask<A>(pid, message, sender)).SequenceParallel();

        [Pure]
        public static Aff<RT, A> ask<A>(ProcessId pid, object message, ProcessId sender) =>
            ask(pid, message, typeof(A), sender).Map(obj => (A)obj);

        [Pure]
        static Aff<RT, A> waitHandle<A>(Func<AutoResetEvent, Aff<RT, A>> f) =>
            AffMaybe<RT, A>(async env => {
                using (var handle = new AutoResetEvent(false))
                {
                    return await f(handle).RunIO(env);
                }
            });

        [Pure]
        static Eff<Unit> waitOne(AutoResetEvent handle, TimeSpan timeout) =>
            Eff(() => {
                handle.WaitOne(timeout);
                return unit;
            });

        [Pure]
        public static Aff<RT, object> ask(ProcessId pid, object message, Type returnType, ProcessId sender) =>
            ActorContextAff<RT>.localSystem(pid,
                from atm in SuccessEff(Atom<AskActorRes>(null))
                from sid in ActorContextAff<RT>.SessionId
                from res in waitHandle<object>(handle => 
                    from req in SuccessEff(new AskActorReq(
                                               message,
                                               res => { atm.Swap(_ => res); handle.Set(); },
                                               pid,
                                               sender,
                                               returnType))
                    from aks in AskItem.Map(ai => ai.Inbox as ILocalActorInbox)
                    from __1 in aks.Tell<RT>(req, sender, sid)
                    from tim in ActorContextAff<RT>.LocalSettings.Map(s => s.Timeout)
                    from __2 in waitOne(handle, tim)
                    from rsp in atm.Value == null   ? throw new TimeoutException("Request timed out")
                              : atm.Value.IsFaulted ? FailEff<object>(atm.Value.Exception)  
                              : SuccessEff(atm.Value.Response)
                    select rsp)
                select res);

        [Pure]
        public static Eff<RT, ProcessName> RootProcessName =>
            ProcessAff<RT>.Root.Map(r => r.Name);

        [Pure]
        public static Eff<RT, Unit> updateSettings(ProcessSystemConfig settings, AppProfile profile) =>
            ActorContextAff<RT>.localSystem(settings.SystemName,
                from sys in ActorContextAff<RT>.LocalSystem
                from res in sys.UpdateSettings(settings, profile)
                select res);

        [Pure]
        public static Aff<RT, ProcessId> actorCreate<S, A>(
            ActorItem parent,
            ProcessName name,
            Func<A, Aff<RT, S>> actorFn,
            Func<S, Aff<RT, Unit>> shutdownFn,
            Func<S, ProcessId, Aff<RT, S>> termFn,
            State<StrategyContext, Unit> strategy,
            ProcessFlags flags,
            int maxMailboxSize,
            bool lazy) =>
                actorCreate<S, A>(
                    parent, 
                    name, 
                    (s, t) => Eff(() => { actorFn(t); return default(S); }), 
                    () => SuccessEff(default(S)), 
                    shutdownFn ?? NoShutdown<S>(), 
                    termFn, 
                    strategy, 
                    flags, 
                    maxMailboxSize, 
                    lazy);

        [Pure]
        public static Aff<RT, ProcessId> actorCreate<S, A>(
            ActorItem parent,
            ProcessName name,
            Action<A> actorFn,
            Func<S, Aff<RT, Unit>> shutdownFn,
            Func<S, ProcessId, Aff<RT, S>> termFn,
            State<StrategyContext, Unit> strategy,
            ProcessFlags flags,
            int maxMailboxSize,
            bool lazy) =>
                actorCreate<S, A>(
                    parent, 
                    name, 
                    (s, t) => Eff(() => { actorFn(t); return default(S); }), 
                    () => SuccessEff(default(S)), 
                    shutdownFn ?? NoShutdown<S>(), 
                    termFn, 
                    strategy, 
                    flags, 
                    maxMailboxSize, 
                    lazy);

        [Pure]
        public static Aff<RT, ProcessId> actorCreate<S, A>(
            ActorItem parent,
            ProcessName name,
            Func<S, A, Aff<RT, S>> actorFn,
            Func<Aff<RT, S>> setupFn,
            Func<S, Aff<RT, Unit>> shutdownFn,
            Func<S, ProcessId, Aff<RT, S>> termFn,
            State<StrategyContext, Unit> strategy,
            ProcessFlags flags,
            int maxMailboxSize,
            bool lazy) =>
                actorCreate(parent, name, actorFn, _ => setupFn(), shutdownFn ?? NoShutdown<S>(), termFn, strategy, flags, maxMailboxSize, lazy);

        [Pure]
        public static Aff<RT, ProcessId> actorCreate<S, A>(ActorItem parent,
            ProcessName name,
            Func<S, A, Aff<RT, S>> actorFn,
            Func<IActor, Aff<RT, S>> setupFn,
            Func<S, Aff<RT, Unit>> shutdownFn,
            Func<S, ProcessId, Aff<RT, S>> termFn,
            State<StrategyContext, Unit> strategy,
            ProcessFlags flags,
            int maxMailboxSize,
            bool lazy) =>
            ActorContextAff<RT>.localSystem(
                parent.Actor.Id,

                from sys in ActorContextAff<RT>.LocalSystem

                // Create the actor
                from act in ActorAff<RT>.create<S, A>(
                    sys.Cluster, // hello
                    parent,      // world
                    name,
                    actorFn,
                    setupFn,
                    shutdownFn ?? NoShutdown<S>(),
                    termFn,
                    strategy,
                    flags)

                // Create the inbox
                from ibx in SuccessEff(sys.Cluster && act.Flags.HasListenRemoteAndLocal() ? new ActorInboxDual<S, A>()
                                     : sys.Cluster && act.Flags.HasPersistInbox()         ? new ActorInboxRemote<S, A>()
                                     : new ActorInboxLocal<S, A>() as IActorInbox)

                // Package in an ActorItem
                from itm in SuccessEff(new ActorItem(act, ibx, act.Flags))

                // Hook up the actor to its parent, register it, and start it
                from res in (from __1 in parent.Actor.LinkChild(itm)
                             from __2 in actorRegister(act)
                             from __3 in SuccessEff(ibx.Startup(act, parent, sys.Cluster, maxMailboxSize))
                             from __4 in lazy
                                             ? unitEff
                                             : ActorSystemAff<RT>.tellSystem(act.Id, SystemMessage.StartupProcess(false))
                             select act.Id)
                           .Match(SuccessEff, e => from _ in act.WithRuntime<RT>().Shutdown(false)
                                                   from r in FailEff<ProcessId>(e)
                                                   select r)
                           .Flatten()
                select res);

        /// <summary>
        /// Register an actor with its registered name from the conf file settings
        /// </summary>
        static Aff<RT, Unit> actorRegister(IActor act) =>
            ProcessSystemConfigAff<RT>.getProcessRegisteredName(act.Id)
                .Match(
                    Succ: regName =>
                        from sys in ActorContextAff<RT>.LocalSystem
                                              
                        // Also check if 'dispatch' is specified in the config, if so we will
                        // register the Process as a role dispatcher PID instead of just its
                        // PID.  
                                              
                        from dsp in ProcessSystemConfigAff<RT>.getProcessRegisteredName(act.Id)
                                        .Match(Succ: disp => ProcessAff<RT>.register(regName, sys.Disp[$"role-{disp}"][Role.Current].Append(act.Id.Skip(1))),
                                               Fail: erro => ProcessAff<RT>.register(regName, act.Id))
                                        .Flatten()
                        select unit,
                    Fail: _ => unitEff)
                .Flatten();      
        
        [Pure]
        public static Eff<RT, ActorItem> JsItem =>
            from chs in RootChildren
            from res in chs.ContainsKey("js")
                            ? SuccessEff(chs["js"])
                            : FailEff<ActorItem>(Error.New($"'js' item doesn't exist"))
            select res;

        [Pure]
        public static Eff<RT, ActorItem> AskItem =>
            from cfg in ActorContextAff<RT>.GlobalSettings
            from res in getSystemItem(cfg.AskProcessName)
            select res;

        [Pure]
        public static Eff<RT, ActorItem> InboxShutdownItem =>
            from cfg in ActorContextAff<RT>.GlobalSettings
            from res in getSystemItem(cfg.InboxShutdownProcessName)
            select res;

        [Pure]
        public static Eff<RT, ActorItem> getSystemItem(ProcessName name) =>
            from chs in RootChildren
            from cfg in ActorContextAff<RT>.GlobalSettings
            from spn in SuccessEff(cfg.SystemProcessName.Value)
            from res in chs.ContainsKey(spn)
                            ? from sys in SuccessEff(chs[spn])
                              from cs2 in SuccessEff(sys.Actor.Children)
                              from isn in SuccessEff(name.Value)
                              from rs2 in cs2.ContainsKey(isn)
                                              ? SuccessEff(cs2[isn])
                                              : FailEff<ActorItem>(Error.New($"{name} item doesn't exist"))
                              select rs2
                            : FailEff<ActorItem>(Error.New("System process doesn't exist"))
            select res;

        [Pure]
        public static Aff<RT, LanguageExt.HashSet<ProcessId>> getLocalRegistered(ProcessName name) =>
            from sys in ActorContextAff<RT>.LocalSystem
            select sys.Registered.Value.RegisteredProcessNames.Find(name).IfNone(HashSet<ProcessId>());

        [Pure]
        static Eff<RT, HashMap<string, ActorItem>> RootChildren =>
            from lsy in ActorContextAff<RT>.LocalSystem
            from chs in SuccessEff(lsy.RootItem?.Actor?.Children ?? HashMap<string, ActorItem>())
            select chs;        

        [Pure]
        public static Aff<RT, ProcessId> register(ProcessName name, ProcessId pid) =>
            ActorContextAff<RT>.localSystem(pid,
                from _   in pid.AssertValid()
                from sys in ActorContextAff<RT>.LocalSystem 
                from res in sys.Cluster
                                ? from disp in getDispatcher(pid)
                                  from loca in disp.IsLocal<RT>()
                                  from res2 in isLocal(pid, sys) && loca
                                                   ? addLocalRegistered(name, pid.SetSystem(sys.SystemName))
                                                   :            // TODO - Make this transactional
                                                     from _1 in Echo.Cluster.setAddOrUpdate<RT, string>(ProcessId.Top["__registered"][name].Path, pid.Path)
                                                     from _2 in Echo.Cluster.setAddOrUpdate<RT, string>(pid.Path + "-registered", name.Value)
                                                     select unit
                                  select unit
                                : addLocalRegistered(name, pid)
                select sys.Disp["reg"][name]);

        [Pure]
        public static Aff<RT, Unit> addLocalRegistered(ProcessName name, ProcessId pid) =>
            ActorContextAff<RT>.localSystem(pid,
                from sys in ActorContextAff<RT>.LocalSystem
                from res in sys.AddLocalRegistered(name, pid)
                select unit);

        [Pure]
        public static Aff<RT, Unit> deregisterByName(ProcessName name) =>
            from sys in ActorContextAff<RT>.LocalSystem
            from res in sys.Cluster
                            ? from _1      in removeLocalRegisteredByName(name)
                              from regpath in SuccessEff((ProcessId.Top["__registered"][name]).Path)

                              // TODO - Make this transactional
                              from pids    in Echo.Cluster.getSet<RT, string>(regpath)
                              from _2      in pids.Map(pid => Echo.Cluster.setRemove<RT, string>($"{pid}-registered", name.Value)).SequenceSerial()
                              from _3      in Echo.Cluster.delete<RT>(regpath)
                              select unit
                            : removeLocalRegisteredByName(name)
            select unit;
        
        [Pure]
        public static Aff<RT, Unit> deregisterById(ProcessId pid) =>
            ActorContextAff<RT>.localSystem(pid,
                from ___ in pid.AssertValid()
                from sys in ActorContextAff<RT>.LocalSystem
                from res in pid.Take(2) == sys.Disp["reg"]
                                ? FailEff<Unit>(new InvalidProcessIdException(deregError))
                                : deregisterByIdInternal(pid, sys)
                select res);

        [Pure]
        static Aff<RT, Unit> deregisterByIdInternal(ProcessId pid, ActorSystem sys) =>
            sys.Cluster && !isLocal(pid, sys)
                ? from dispatch in getDispatcher(pid)
                  from islocal  in dispatch.IsLocal<RT>()
                  from result   in islocal
                                       ? removeLocalRegisteredById(pid)
                                       : from _1      in removeLocalRegisteredById(pid)
                                         from path    in SuccessEff(pid.Path)
                                         from regpath in SuccessEff($"{path}-registered")
                                                      // TODO - Make this transactional
                                         from names   in Echo.Cluster.getSet<RT, string>(regpath)
                                         from _2      in names.Map(n => Echo.Cluster.setRemove<RT, string>(ProcessId.Top["__registered"][n].Path, path)).SequenceSerial()
                                         from _3      in Echo.Cluster.delete<RT>(regpath)
                                         select unit
                  select result
                : removeLocalRegisteredById(pid);

        [Pure]
        static Eff<RT, Unit> removeLocalRegisteredById(ProcessId pid) =>
            ActorContextAff<RT>.localSystem(pid,
                from sys in ActorContextAff<RT>.LocalSystem
                from res in sys.RemoveLocalRegisteredById(pid)
                select unit);

        [Pure]
        static Eff<RT, Unit> removeLocalRegisteredByName(ProcessName name) =>
            from sys in ActorContextAff<RT>.LocalSystem
            from res in sys.RemoveLocalRegisteredByName(name)
            select unit;

        [Pure]
        public static Aff<RT, A> withContext<A>(ActorItem self, Aff<RT, A> ma) =>
            ActorContextAff<RT>.localSystem(
                self.Actor.Id,
                from sys in ActorContextAff<RT>.LocalSystem
                from res in ActorContextAff<RT>.localContext(new ActorRequestContext(sys, self, ProcessId.NoSender, self.Actor.Parent, null, null, self.Actor.Flags), ma)
                select res);

        [Pure]
        public static Eff<RT, A> withContext<A>(ActorItem self, Eff<RT, A> ma) =>
            ActorContextAff<RT>.localSystem(
                self.Actor.Id,
                from sys in ActorContextAff<RT>.LocalSystem
                from res in ActorContextAff<RT>.localContext(new ActorRequestContext(sys, self, ProcessId.NoSender, self.Actor.Parent, null, null, self.Actor.Flags), ma)
                select res);
 
        [Pure]
        public static Aff<RT, A> withContext<A>(
            ActorItem self,
            ProcessId sender,
            ActorRequest request,
            object msg,
            Option<SessionId> sessionId,
            Aff<RT, A> ma) =>
                ActorContextAff<RT>.localSystem(
                    self.Actor.Id,
                    from sys in ActorContextAff<RT>.LocalSystem
                    from res in ActorContextAff<RT>.localContext(new ActorRequestContext(sys, self, sender, self.Actor.Parent, msg, request, self.Actor.Flags), ma)
                    select res);

        [Pure]
        public static Eff<RT, A> withContext<A>(ActorItem self,
            ProcessId sender,
            ActorRequest request,
            object msg,
            Option<SessionId> sessionId,
            Eff<RT, A> ma) =>
                ActorContextAff<RT>.localSystem(
                    self.Actor.Id,
                    from sys in ActorContextAff<RT>.LocalSystem
                    from res in ActorContextAff<RT>.localContext(new ActorRequestContext(sys, self, sender, self.Actor.Parent, msg, request, self.Actor.Flags), ma)
                    select res);

        [Pure]
        internal static Aff<RT, IObservable<A>> observe<A>(ProcessId pid) =>
            getDispatcher(pid).Bind(d => d.Observe<RT, A>());

        [Pure]
        internal static Aff<RT, IObservable<A>> observeState<A>(ProcessId pid) =>
            getDispatcher(pid).Bind(d => d.ObserveState<RT, A>());

        [Pure]
        internal static Eff<RT, ProcessId> senderOrDefault(ProcessId sender) =>
            sender.IsValid
                ? SuccessEff(sender)
                : ProcessAff<RT>.Self;

        [Pure]
        static IActorDispatch getJsDispatcher(ProcessId pid, ProcessSystemConfig settings, Option<SessionId> sid) =>
            new ActorDispatchJS(pid, sid, settings.TransactionalIO);

        [Pure]
        static IActorDispatch getLocalDispatcher(ProcessId pid, ActorSystem sys, Option<SessionId> sid) =>
            pid.Take(2) == sys.RootJS
                ? getJsDispatcher(pid, sys.Settings, sid)
                : getDispatcher(pid.Tail(), sys.RootItem, pid, sys, sid);

        [Pure]
        static IActorDispatch getRemoteDispatcher(ProcessId pid, ActorSystem sys, Option<SessionId> sid) =>
            sys.Cluster
                ? new ActorDispatchRemote(sys.Ping, pid, sid, sys.Settings.Value.TransactionalIO) as IActorDispatch
                : new ActorDispatchNotExist(pid);

        [Pure]
        internal static Option<Func<ProcessId, G.IEnumerable<ProcessId>>> getProcessSelector(ProcessId pid)
        {
            if (pid.Count() < 3) throw new InvalidProcessIdException("Invalid role Process ID");
            var type = pid.Skip(1).Take(1).Name;
            return Dispatch.getFunc(type);
        }

        [Pure]
        public static G.IEnumerable<ProcessId> resolveProcessIdSelection(ProcessId pid) =>
            getProcessSelector(pid)
                .Map(selector => selector(pid.Skip(2)))
                .IfNone(() => new ProcessId[0]);

        [Pure]
        static IActorDispatch getPluginDispatcher(ProcessId pid, ProcessSystemConfig settings) =>
            getProcessSelector(pid)
                .Map(selector => new ActorDispatchGroup(selector(pid.Skip(2)), settings.TransactionalIO) as IActorDispatch)
                .IfNone(() => new ActorDispatchNotExist(pid));

        [Pure]
        static bool isLocal(ProcessId pid, ActorSystem sys) =>
            pid.StartsWith(sys.Root);

        [Pure]
        static bool isDisp(ProcessId pid) =>
            pid.value.IsDisp;

        [Pure]
        public static Eff<RT, IActorDispatch> getDispatcher(ProcessId pid) =>
            ActorContextAff<RT>.localSystem(pid,
                from sys in ActorContextAff<RT>.LocalSystem
                from sid in ActorContextAff<RT>.SessionId.Match(Some, _ => None)
                select pid.IsValid
                        ? pid.IsSelection
                            ? new ActorDispatchGroup(pid.GetSelection(), sys.Settings.Value.TransactionalIO)
                            : isDisp(pid)
                                ? getPluginDispatcher(pid, sys.Settings)
                                : isLocal(pid, sys)
                                    ? getLocalDispatcher(pid, sys, sid)
                                    : getRemoteDispatcher(pid, sys, sid)
                        : new ActorDispatchNotExist(pid));

        [Pure]
        static IActorDispatch getDispatcher(ProcessId pid, ActorItem current, ProcessId orig, ActorSystem sys, Option<SessionId> sessionId)
        {
            if (pid == ProcessId.Top)
            {
                if (current.Inbox is ILocalActorInbox)
                {
                    return new ActorDispatchLocal(current, sys.Settings.Value.TransactionalIO, sessionId);
                }
                else
                {
                    return sys.Cluster
                            ? new ActorDispatchRemote(sys.Ping, orig, sessionId, sys.Settings.Value.TransactionalIO) as IActorDispatch
                            : new ActorDispatchNotExist(orig);
                }
            }
            else
            {
                var child = pid.HeadName().Value;
                return current.Actor.Children.Find(child,
                    Some: process => getDispatcher(pid.Tail(), process, orig, sys, sessionId),
                    None: ()      => new ActorDispatchNotExist(orig));
            }
        }

        [Pure]
        public static Aff<RT, Unit> ask(ProcessId pid, object message, ProcessId sender) =>
            ActorContextAff<RT>.localSystem(pid,
                from d in getDispatcher(pid)
                from s in senderOrDefault(sender)
                from r in d.Ask<RT>(message, s)
                select r);

        [Pure]
        public static Aff<RT, Unit> tell(ProcessId pid, object message, Schedule schedule, ProcessId sender) =>
            ActorContextAff<RT>.localSystem(pid,
                from d in getDispatcher(pid)
                from s in senderOrDefault(sender)
                from r in d.Tell<RT>(message, schedule, s, message is ActorRequest ? Message.TagSpec.UserAsk : Message.TagSpec.User)
                select r);

        [Pure]
        public static Aff<RT, Unit> tell(Eff<RT, ProcessId> pid, object message, Schedule schedule, ProcessId sender) =>
            from p in pid
            from r in tell(p, message, schedule, sender)
            select r;

        [Pure]
        public static Aff<RT, Unit> tellUserControl(ProcessId pid, UserControlMessage message) =>
            ActorContextAff<RT>.localSystem(pid,
                from d in getDispatcher(pid)
                from s in ProcessAff<RT>.Self
                from r in d.TellUserControl<RT>(message, s)
                select r);

        [Pure]
        public static Aff<RT, Unit> tellUserControl(Eff<RT, ProcessId> pid, UserControlMessage message) =>
            from p in pid
            from r in tellUserControl(p, message)
            select r;

        [Pure]
        public static Aff<RT, Unit> tellSystem(ProcessId pid, SystemMessage message) =>
            ActorContextAff<RT>.localSystem(pid,
                from d in getDispatcher(pid)
                from s in ProcessAff<RT>.Self
                from r in d.TellSystem<RT>(message, s)
                select r);

        [Pure]
        public static Aff<RT, Unit> tellSystem(Eff<RT, ProcessId> pid, SystemMessage message) =>
            from p in pid
            from r in tellSystem(p, message)
            select r;

        [Pure]
        public static Aff<RT, HashMap<string, ProcessId>> getChildren(ProcessId pid) =>
            ActorContextAff<RT>.localSystem(pid,
                from d in getDispatcher(pid)
                from r in d.GetChildren<RT>()
                select r);

        [Pure]
        public static Aff<RT, Unit> kill(ProcessId pid, bool maintainState) =>
            ActorContextAff<RT>.localSystem(pid,
                maintainState
                    ? getDispatcher(pid).Bind(d => d.Shutdown<RT>())
                    : getDispatcher(pid).Bind(d => d.Kill<RT>()));

        [Pure]
        public static Eff<RT, Option<ActorItem>> localActor(ProcessId pid) =>
            ActorContextAff<RT>.localSystem(pid,
                from sys in ActorContextAff<RT>.LocalSystem
                from res in pid.System == sys.SystemName
                                ? SuccessEff(localActor(sys.RootItem, pid.Skip(1), pid))
                                : SuccessEff(Option<ActorItem>.None)
                select res); 

        [Pure]
        static Option<ActorItem> localActor(ActorItem current, ProcessId walk, ProcessId pid)
        {
            if (current.Actor.Id == pid) return current;
            var name = walk.Take(1).Name;
            return from child in current.Actor.Children.Find(walk.Take(1).Name.Value)
                   from result in localActor(child, walk.Skip(1), pid)
                   select result;
        }

        /// <summary>
        /// Get the name to use to register the Process
        /// </summary>
        [Pure]
        static Aff<RT, A> getProcessSetting<A>(ProcessId pid, string name) =>
            ActorContextAff<RT>.localSystem(pid,
                ProcessSystemConfigAff<RT>.getProcessSetting<A>(pid, name, "value"));

        /// <summary>
        /// Get the name to use to register the Process
        /// </summary>
        [Pure]
        public static Aff<RT, ProcessName> getProcessRegisteredName(ProcessId pid) =>
            getProcessSetting<ProcessName>(pid, "register-as"))

        /// <summary>
        /// Get the dispatch method
        /// This is used by the registration system, it registers the Process 
        /// using a Role[dispatch][...relative path to process...]
        /// </summary>
        [Pure]
        public static Aff<RT, string> getProcessDispatch(ProcessId pid) =>
            getProcessSetting<string>(pid, "dispatch");

        /// <summary>
        /// Get the router dispatch method
        /// This is used by routers to specify the type of routing
        /// </summary>
        [Pure]
        public static Aff<RT, string> getRouterDispatch(ProcessId pid) =>
            getProcessSetting<string>(pid, "route");

        /// <summary>
        /// Get the router workers list
        /// </summary>
        [Pure]
        public static Aff<RT, Seq<ProcessToken>> getRouterWorkers(ProcessId pid) =>
            getProcessSetting<Seq<ProcessToken>>(pid, "workers")
                .Match(SuccessEff, _ => SuccessEff(Seq<ProcessToken>()))
                .Flatten();

        /// <summary>
        /// Get the router worker count
        /// </summary>
        [Pure]
        public static Aff<RT, int> getRouterWorkerCount(ProcessId pid) =>
            getProcessSetting<int>(pid, "worker-count")
                .Match(SuccessEff, _ => SuccessEff(2))
                .Flatten();

        /// <summary>
        /// Get the router worker name
        /// </summary>
        [Pure]
        public static Aff<RT, string> getRouterWorkerName(ProcessId pid) =>
            getProcessSetting<string>(pid, "worker-name")
                .Match(SuccessEff, _ => SuccessEff("worker"))
                .Flatten();
        
        /// <summary>
        /// Get the mailbox size for a Process.  Returns a default size if one
        /// hasn't been set in the config.
        /// </summary>
        [Pure]
        public static Aff<RT, int> getProcessMailboxSize(ProcessId pid) =>
            getProcessSetting<int>(pid, "mailbox-size")
                .Match(Succ: SuccessEff,
                       Fail: _ => ActorContextAff<RT>.LocalSystem.Map(sys => sys.Settings.Value.MaxMailboxSize))
                .Flatten();

        /// <summary>
        /// Get the flags for a Process.  Returns ProcessFlags.Default if none
        /// have been set in the config.
        /// </summary>
        [Pure]
        public static Aff<RT, ProcessFlags> getProcessFlags(ProcessId pid) =>
            getProcessSetting<ProcessFlags>(pid, "flags")
                .Match(Succ: SuccessEff,
                       Fail: _ => SuccessEff(ProcessFlags.Default))
                .Flatten();

        /// <summary>
        /// Get the strategy for a Process.  Returns Process.DefaultStrategy if one
        /// hasn't been set in the config.
        /// </summary>
        [Pure]
        public static Aff<RT, State<StrategyContext, Unit>> getProcessStrategy(ProcessId pid) =>
            getProcessSetting< State<StrategyContext, Unit>>(pid, "strategy")
                .Match(Succ: SuccessEff,
                       Fail: _ => SuccessEff(Process.DefaultStrategy))
                .Flatten();        

        const string deregError = @"
When de-registering a Process, you should use its actual ProcessId, not its registered
ProcessId.  Multiple Processes can be registered with the same name, and therefore share
the same registered ProcessId.  The de-register system can only know for sure which Process
to de-register if you pass in the actual ProcessId.  If you want to deregister all Processes
by name then use Process.deregisterByName(name).";
        
    }
}
