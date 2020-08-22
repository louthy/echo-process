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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;

namespace Echo
{
    class WatchState
    {
        public static readonly WatchState Empty = new WatchState(default, default);
         
        public readonly HashMap<ProcessId, LanguageExt.HashSet<ProcessId>> Watchers;
        public readonly HashMap<ProcessId, LanguageExt.HashSet<ProcessId>> Watchings;

        WatchState(
            HashMap<ProcessId, LanguageExt.HashSet<ProcessId>> watchers,
            HashMap<ProcessId, LanguageExt.HashSet<ProcessId>> watchings) =>
            (Watchers, Watchings) = (watchers, watchings);
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
        public readonly ActorRequestContext UserContext;
        public readonly IDisposable StartupSubscription;
        public readonly ActorItem RootItem;
        public readonly bool Cluster;
        public readonly long StartupTimestamp;
        public readonly SessionManager SessionManager;
        public readonly Ping Ping;
        public readonly SystemName SystemName;
        
        // TODO: Below not yet constructed
        
        public ILocalActorInbox LocalRoot => (ILocalActorInbox)rootItem.Inbox;
        public IActorInbox RootInbox => rootItem.Inbox;
        public ProcessId Root { get; }
        public ProcessId User { get; }
        public ProcessId Errors { get; }
        public ProcessId Scheduler { get; }
        public ProcessId DeadLetters { get; }
        public ProcessId Disp { get; }
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
            ActorRequestContext userContext, 
            IDisposable startupSubscription, 
            ActorItem rootItem, 
            bool cluster,
            long startupTimestamp, 
            SessionManager sessionManager, 
            Ping ping, 
            SystemName systemName)
        {
            ClusterState = clusterState;
            Watch = watch;
            Registered = registered;
            Settings = settings;
            AppProfile = appProfile;
            UserContext = userContext;
            StartupSubscription = startupSubscription;
            RootItem = rootItem;
            Cluster = cluster;
            StartupTimestamp = startupTimestamp;
            SessionManager = sessionManager;
            Ping = ping;
            SystemName = systemName;
        }

        public EffPure<Unit> UpdateSettings(ProcessSystemConfig settings, AppProfile profile) =>
            from _1 in Settings.SwapEff(_ => SuccessEff(settings))
            from _2 in AppProfile.SwapEff(_ => SuccessEff(profile))
            select unit;

        public EffPure<Unit> ApplyUpdate(SettingOverride update) =>
            from _ in swapEff(Settings, s => SuccessEff(s.With(SettingOverrides: s.SettingOverrides.ApplyUpdate(update))))
            select unit;

        public EffPure<Unit> ApplyUpdates(SettingOverrideUpdates updates) =>
            from _ in swapEff(Settings, s => SuccessEff(s.With(SettingOverrides: s.SettingOverrides.ApplyUpdates(updates))))
            select unit;

        public EffPure<Unit> ApplyUpdates<A>(Option<SettingOverrideUpdates> ma) =>
            ma.Match(
                Some: ApplyUpdates,
                None: () => unitEff);

        public EffPure<RegisteredState> RemoveLocalRegisteredByName(ProcessName name) =>
            swapEff(Registered, s => SuccessEff(s.RemoveLocalRegisteredByName(name)));

        public EffPure<RegisteredState> RemoveLocalRegisteredById(ProcessId pid) =>
            swapEff(Registered, s => SuccessEff(s.RemoveLocalRegisteredById(pid)));

        public EffPure<RegisteredState> AddLocalRegistered(ProcessName name, ProcessId pid) =>
            swapEff(Registered, s => SuccessEff(s.AddLocalRegistered(name, pid, SystemName)));
    }

    static class ActorSystemAff<RT> 
        where RT : struct, HasEcho<RT>
    {
        const string ClusterOnlineKey = "cluster-node-online";

        public static Func<S, Aff<RT, Unit>> NoShutdown<S>() => (S s) => SuccessAff(unit);

        /*enum DisposeState
        {
            Active,
            Disposing,
            Disposed
        }

        DisposeState disposeState;*/


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

        IDisposable NotifyCluster(Option<ICluster> cluster, long timestamp) =>
            cluster.Map(c =>
            {
                c.PublishToChannel(ClusterOnlineKey, new ClusterOnline { Name = c.NodeName.Value, Timestamp = timestamp });
                return c.SubscribeToChannel<ClusterOnline>(ClusterOnlineKey)
                        .Where(node => node.Name == c.NodeName.Value && node.Timestamp > timestamp)
                        .Take(1)
                        .Subscribe(_ => shutdownAll()); // Protects against multiple nodes with the same name running
            })
           .IfNone(() => Observable.Return(new ClusterOnline()).Take(1).Subscribe());

        public ClusterMonitor.State ClusterState =>
            clusterState;

        void ClusterWatch(Option<ICluster> cluster)
        {
            var monitor = System[ActorSystemConfig.Default.MonitorProcessName];

            cluster.IfSome(c =>
            {
                observe<NodeOnline>(monitor).Subscribe(x =>
                {
                    logInfo("Online: " + x.Name);
                });

                observe<NodeOffline>(monitor).Subscribe(x =>
                {
                    logInfo("Offline: " + x.Name);
                    RemoveWatchingOfRemote(x.Name);
                });

                observeState<ClusterMonitor.State>(monitor).Subscribe(x =>
                {
                    clusterState = x;
                });
            });

            Tell(monitor, new ClusterMonitor.Msg(ClusterMonitor.MsgTag.Heartbeat), Schedule.Immediate, User);
        }

        void SchedulerStart(Option<ICluster> cluster)
        {
            var pid = System[ActorSystemConfig.Default.SchedulerName];
            cluster.IfSome(c => Tell(pid, Echo.Scheduler.Msg.Check, Schedule.Immediate, User));
        }

        public Aff<RT, Unit> Dispose<RT>() where RT : struct, HasEcho<RT>
        {
            if (disposeState != DisposeState.Active) return;
            lock (sync)
            {
                if (disposeState != DisposeState.Active) return;
                disposeState = DisposeState.Disposing;

                if (rootItem != null)
                {
                    try
                    {
                        Ping.Dispose();
                    }
                    catch (Exception e)
                    {
                        logErr(e);
                    }
                    try
                    {
                        startupSubscription?.Dispose();
                        startupSubscription = null;
                    }
                    catch (Exception e)
                    {
                        logErr(e);
                    }
                    try
                    {
                        rootItem.Actor.Children.Values
                            .OrderByDescending(c => c.Actor.Id == User) // shutdown "user" first
                            .Iter(c => c.Actor.ShutdownProcess(true));
                    }
                    catch(Exception e)
                    {
                        logErr(e);
                    }
                    cluster.IfSome(c => c?.Dispose());
                }
                rootItem = null;
                disposeState = DisposeState.Disposed;
            }
        }

        private Unit RemoveWatchingOfRemote(ProcessName node)
        {
            var root = ProcessId.Top[node];

            foreach (var watching in watchings)
            {
                if (watching.Key.Take(1) == root)
                {
                    RemoteDispatchTerminate(watching.Key);
                }
            }
            return unit;
        }

        public static Eff<RT, Unit> addWatcher(ProcessId watcher, ProcessId watching)
        {
            logInfo(watcher + " is watching " + watching);

            lock (sync)
            {
                watchers = watchers.AddOrUpdate(watching,
                    Some: set => set.AddOrUpdate(watcher),
                    None: () => Set(watcher)
                );

                watchings = watchings.AddOrUpdate(watcher,
                    Some: set => set.AddOrUpdate(watching),
                    None: () => Set(watching)
                );
            }
            return unit;
        }

        public static Eff<RT, Unit> removeWatcher(ProcessId watcher, ProcessId watching)
        {
            logInfo(watcher + " stopped watching " + watching);

            lock (sync)
            {
                watchers = watchers.AddOrUpdate(watching,
                    Some: set => set.Remove(watcher),
                    None: () => Set<ProcessId>()
                );

                if (watchers[watching].IsEmpty)
                {
                    watchers = watchers.Remove(watching);
                }

                watchings = watchings.AddOrUpdate(watcher,
                    Some: set => set.Remove(watching),
                    None: () => Set<ProcessId>()
                );

                if (watchings[watcher].IsEmpty)
                {
                    watchers = watchers.Remove(watcher);
                }

            }
            return unit;
        }

        public Unit RemoteDispatchTerminate(ProcessId terminating)
        {
            watchings.Find(terminating).IfSome(ws =>
            {
                var term = new TerminatedMessage(terminating);
                ws.Iter(w =>
                {
                    try
                    {
                        TellUserControl(w, term);
                    }
                    catch (Exception e)
                    {
                        logErr(e);
                    }
                });
            });
            watchings.Remove(terminating);
            watchers = watchers.Remove(terminating);

            return unit;
        }

        public static Aff<RT, Unit> dispatchTerminate(ProcessId terminating)
        {
            watchers.Find(terminating).IfSome(ws =>
            {
                var term = new TerminatedMessage(terminating);
                ws.Iter(w =>
                {
                    try
                    {
                        TellUserControl(w, term);
                    }
                    catch (Exception e)
                    {
                        logErr(e);
                    }
                });
            });

            watchers = watchers.Remove(terminating);

            watchings.Find(terminating).IfSome(ws => ws.Iter(w => GetDispatcher(w).UnWatch(terminating)));
            watchings.Remove(terminating);

            return unit;
        }

        public Option<ICluster> Cluster =>
            cluster;

        public IEnumerable<T> AskMany<T>(IEnumerable<ProcessId> pids, object message, int take)
        {
            take = Math.Min(take, pids.Count());

            var responses = new List<AskActorRes>();
            using (var handle = new CountdownEvent(take))
            {
                foreach (var pid in pids)
                {
                    var req = new AskActorReq(
                        message,
                        res =>
                        {
                            responses.Add(res);
                            handle.Signal();
                        },
                        pid.SetSystem(SystemName),
                        Self,
                        typeof(T));
                    tell(AskId, req);
                }

                handle.Wait(ActorContext.System(pids.Head()).Settings.Timeout);
            }
            return responses.Where(r => !r.IsFaulted).Map(r => (T)r.Response);
        }

        public T Ask<T>(ProcessId pid, object message, ProcessId sender) =>
            (T)Ask(pid, message, typeof(T), sender);

        public object Ask(ProcessId pid, object message, Type returnType, ProcessId sender)
        {
            if (false) //Process.InMessageLoop)
            {
                //return SelfProcess.Actor.ProcessRequest<T>(pid, message);
            }
            else
            {
                var sessionId = ActorContext.SessionId;
                AskActorRes response = null;
                using (var handle = new AutoResetEvent(false))
                {
                    var req = new AskActorReq(
                        message,
                        res => {
                            response = res;
                            handle.Set();
                        },
                        pid,
                        sender,
                        returnType
                    );

                    var askItem = GetAskItem();

                    askItem.IfSome(
                        ask =>
                        {
                            var inbox = ask.Inbox as ILocalActorInbox;
                            inbox.Tell(req, sender, sessionId);
                            handle.WaitOne(ActorContext.System(pid).Settings.Timeout);
                        });

                    if (askItem)
                    {
                        if (response == null)
                        {
                            throw new TimeoutException("Request timed out");
                        }
                        else
                        {
                            if (response.IsFaulted)
                            {
                                throw response.Exception;
                            }
                            else
                            {
                                return response.Response;
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Ask process doesn't exist");
                    }
                }
            }
        }

        public static Eff<RT, ProcessName> RootProcessName =>
            ProcessAff<RT>.Root.Map(r => r.Name);

        public static Eff<RT, Unit> updateSettings(ProcessSystemConfig settings, AppProfile profile) =>
            from sys in ActorContextAff<RT>.LocalSystem
            from res in sys.UpdateSettings(settings, profile)
            select res;

        public static Aff<RT, ProcessId> actorCreate<S, T>(
            ActorItem parent,
            ProcessName name,
            Func<T, Aff<RT, S>> actorFn,
            Func<S, Aff<RT, Unit>> shutdownFn,
            Func<S, ProcessId, Aff<RT, S>> termFn,
            State<StrategyContext, Unit> strategy,
            ProcessFlags flags,
            int maxMailboxSize,
            bool lazy) =>
                actorCreate<S, T>(
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
        
        public static Aff<RT, ProcessId> actorCreate<S, A>(
            ActorItem parent,
            ProcessName name,
            Func<S, A, Aff<RT, S>> actorFn,
            Func<IActor, Aff<RT, S>> setupFn,
            Func<S, Aff<RT, Unit>> shutdownFn,
            Func<S, ProcessId, Aff<RT, S>> termFn,
            State<StrategyContext, Unit> strategy,
            ProcessFlags flags,
            int maxMailboxSize,
            bool lazy) =>
                from sys in ActorContextAff<RT>.LocalSystem
                from act in ActorAff<RT>.create<S, A>(
                    sys.Cluster,
                    parent, 
                    name, 
                    actorFn, 
                    setupFn, 
                    shutdownFn ?? NoShutdown<S>(), 
                    termFn, 
                    strategy, 
                    flags)
                from ibx in SuccessEff(sys.Cluster && act.Flags.HasListenRemoteAndLocal() ? new ActorInboxDual<S, A>() as IActorInbox
                                     : sys.Cluster && act.Flags.HasPersistInbox()         ? new ActorInboxRemote<S, A>() as IActorInbox
                                     : new ActorInboxLocal<S, A>() as IActorInbox)
                from itm in SuccessEff(new ActorItem(act, ibx, act.Flags))

                from res in  (from __1 in parent.Actor.LinkChild(itm)
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
                select res;


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
            select sys.Disp["reg"][name];

        [Pure]
        public static Aff<RT, Unit> addLocalRegistered(ProcessName name, ProcessId pid) =>
            from sys in ActorContextAff<RT>.LocalSystem
            from res in sys.AddLocalRegistered(name, pid)
            select unit;

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
            from ___ in pid.AssertValid()
            from sys in ActorContextAff<RT>.LocalSystem
            from res in pid.Take(2) == sys.Disp["reg"]
                            ? FailEff<Unit>(new InvalidProcessIdException(deregError))
                            : deregisterByIdInternal(pid, sys)
            select res;

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
        static Eff<RT, Unit> removeLocalRegisteredById(ProcessId pid)
            =>
            from sys in ActorContextAff<RT>.LocalSystem
            from res in sys.RemoveLocalRegisteredById(pid)
            select unit;

        [Pure]
        static Eff<RT, Unit> removeLocalRegisteredByName(ProcessName name)
            =>
            from sys in ActorContextAff<RT>.LocalSystem
            from res in sys.RemoveLocalRegisteredByName(name)
            select unit;

        [Pure]
        public static Aff<RT, A> withContext<A>(ActorItem self,
            ActorItem parent,
            ProcessId sender,
            ActorRequest request,
            object msg,
            Option<SessionId> sessionId,
            Aff<RT, A> ma) =>
                from sys in ActorContextAff<RT>.LocalSystem
                from res in ActorContextAff<RT>.localContext(new ActorRequestContext(sys, self, sender, parent, msg, request, ProcessFlags.Default), ma)
                select res;

        [Pure]
        public static Eff<RT, A> withContext<A>(ActorItem self,
            ActorItem parent,
            ProcessId sender,
            ActorRequest request,
            object msg,
            Option<SessionId> sessionId,
            Eff<RT, A> ma) =>
                from sys in ActorContextAff<RT>.LocalSystem
                from res in ActorContextAff<RT>.localContext(new ActorRequestContext(sys, self, sender, parent, msg, request, ProcessFlags.Default), ma)
                select res;

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
        internal static Option<Func<ProcessId, IEnumerable<ProcessId>>> getProcessSelector(ProcessId pid)
        {
            if (pid.Count() < 3) throw new InvalidProcessIdException("Invalid role Process ID");
            var type = pid.Skip(1).Take(1).Name;
            return Dispatch.getFunc(type);
        }

        [Pure]
        public static IEnumerable<ProcessId> resolveProcessIdSelection(ProcessId pid) =>
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
            from d in getDispatcher(pid)
            from s in senderOrDefault(sender)
            from r in d.Ask<RT>(message, s)
            select r;

        [Pure]
        public static Aff<RT, Unit> tell(ProcessId pid, object message, Schedule schedule, ProcessId sender) =>
            from d in getDispatcher(pid)
            from s in senderOrDefault(sender)
            from r in d.Tell<RT>(message, schedule, s, message is ActorRequest ? Message.TagSpec.UserAsk : Message.TagSpec.User)
            select r;

        [Pure]
        public static Aff<RT, Unit> tell(Eff<RT, ProcessId> pid, object message, Schedule schedule, ProcessId sender) =>
            from p in pid
            from d in getDispatcher(p)
            from s in senderOrDefault(sender)
            from r in d.Tell<RT>(message, schedule, s, message is ActorRequest ? Message.TagSpec.UserAsk : Message.TagSpec.User)
            select r;

        [Pure]
        public static Aff<RT, Unit> tellUserControl(ProcessId pid, UserControlMessage message) =>
            from d in getDispatcher(pid)
            from s in ProcessAff<RT>.Self
            from r in d.TellUserControl<RT>(message, s)
            select r;

        [Pure]
        public static Aff<RT, Unit> tellUserControl(Eff<RT, ProcessId> pid, UserControlMessage message) =>
            from p in pid
            from d in getDispatcher(p)
            from s in ProcessAff<RT>.Self
            from r in d.TellUserControl<RT>(message, s)
            select r;

        [Pure]
        public static Aff<RT, Unit> tellSystem(ProcessId pid, SystemMessage message) =>
            from d in getDispatcher(pid)
            from s in ProcessAff<RT>.Self
            from r in d.TellSystem<RT>(message, s)
            select r;

        [Pure]
        public static Aff<RT, Unit> tellSystem(Eff<RT, ProcessId> pid, SystemMessage message) =>
            from p in pid
            from d in getDispatcher(p)
            from s in ProcessAff<RT>.Self
            from r in d.TellSystem<RT>(message, s)
            select r;

        [Pure]
        public static Aff<RT, HashMap<string, ProcessId>> getChildren(ProcessId pid) =>
            from d in getDispatcher(pid)
            from r in d.GetChildren<RT>()
            select r;

        [Pure]
        public static Aff<RT, Unit> kill(ProcessId pid, bool maintainState) =>
            maintainState
                ? getDispatcher(pid).Bind(d => d.Shutdown<RT>())
                : getDispatcher(pid).Bind(d => d.Kill<RT>());

        [Pure]
        public static Eff<RT, Option<ActorItem>> localActor(ProcessId pid) =>
            from sys in ActorContextAff<RT>.LocalSystem
            from res in pid.System == sys.SystemName
                            ? SuccessEff(localActor(sys.RootItem, pid.Skip(1), pid))
                            : SuccessEff(Option<ActorItem>.None)
            select res; 

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
            ProcessSystemConfigAff<RT>.getProcessSetting<A>(pid, name, "value");

        /// <summary>
        /// Get the name to use to register the Process
        /// </summary>
        [Pure]
        public static Aff<RT, ProcessName> getProcessRegisteredName(ProcessId pid) =>
            getProcessSetting<ProcessName>(pid, "register-as");

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
