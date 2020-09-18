using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.Remoting.Contexts;
using static LanguageExt.Prelude;
using Echo.Config;
using LanguageExt;
using System.Threading;
using Echo.Session;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;

namespace Echo
{
    static class ActorContext
    {
        readonly static Atom<SystemName> defaultSystem = Atom<SystemName>(default);
        readonly static Atom<Seq<ActorSystem>> systems = Atom(Seq<ActorSystem>());
        
        public static Eff<Seq<ActorSystem>> Systems => 
            SuccessEff(systems.Value);
        
        public static Eff<Seq<SystemName>> SystemNames => 
            SuccessEff(systems.Value.Map(s => s.SystemName));

        public static Eff<Unit> addOrUpdateSystem(ActorSystem system) =>
            systems.SwapEff(syss => SuccessEff(system.Cons(syss.Filter(s => s.SystemName != system.SystemName))))
                   .Map(_ => unit);

        public static Eff<Unit> removeSystem(SystemName system) =>
            from _1 in clearDefaultSystemIfMatch(system)
            from _2 in systems.SwapEff(syss => SuccessEff(syss.Filter(s => s.SystemName != system)))
            select unit; 

        public static Eff<Unit> setDefaultSystem(SystemName defaultSys) =>
            defaultSystem.SwapEff(ds => SuccessEff(defaultSys))
                         .Map(_ => unit);

        public static Eff<Unit> clearDefaultSystemIfMatch(SystemName comparand) =>
            defaultSystem.SwapEff(ds => SuccessEff(ds == comparand ? default : ds))
                         .Map(_ => unit);

        public static Eff<bool> systemExists(SystemName system) =>
            findSystem(system).Match(Succ: _ => true, Fail: _ => false);

        public static Eff<ActorSystem> findSystem(SystemName system) =>
            EffMaybe<ActorSystem>(() => {
                foreach (var item in systems.Value)
                {
                    if (item.SystemName == system) return item;
                }
                return Error.New($"Process-system does not exist: {system}");
            });
       

        public static Eff<ActorSystem> system(ProcessId pid) =>
            findSystem(pid.System);

        public static Eff<ActorSystem> system(SystemName system) =>
            findSystem(system) | DefaultSystem;

        public static Eff<ActorSystem> DefaultSystem =>
            findSystem(defaultSystem);
    }

    static class ActorContextAff<RT> 
        where RT : struct, HasEcho<RT>
    {
        public static Aff<RT, Unit> startSystem(SystemName system, bool cluster, AppProfile appProfile, ProcessSystemConfig config) =>
            from exists in ActorContext.systemExists(system)
            from result in exists
                               ? FailEff<Unit>(Error.New($"Process-system ({system}) already started"))
                               : startSystemInternal(system, cluster, appProfile, config)
            select result;

        static Eff<Unit> assertSystemNotExist(SystemName system) =>
            ActorContext.findSystem(system)
                        .Match(Succ: FailEff<Unit>(Error.New($"Process-system ({system}) already started")),
                               Fail: SuccessEff(unit));
        
        static Aff<RT, Unit> startSystemInternal(SystemName system, bool cluster, AppProfile appProfile, ProcessSystemConfig config) =>
            from __1 in assertSystemNotExist(system)
            from not in ActorSystemAff<RT>.notifyCluster(cluster, system, DateTime.UtcNow.Ticks)
            from sys in SuccessEff(
                            new ActorSystem(
                                Atom(ClusterMonitor.State.Empty(system)),
                                Atom(WatchState.Empty),
                                Atom(RegisteredState.Empty),
                                Atom(config),
                                Atom(appProfile),
                                not,
                                null,
                                cluster,
                                DateTime.UtcNow.Ticks,
                                new SessionManager(cluster, system, appProfile.NodeName, VectorConflictStrategy.Branch),
                                new Ping(system),
                                system))
            from __2 in ActorContext.addOrUpdateSystem(sys)
            from __3 in ActorContextAff<RT>.localSystem(system,  systemBootstrap)
            select unit;
                
        
            // TODO: Convert code below into Aff LINQ
        
        //
        // {
        //     if (SystemExists(system))
        //     {
        //         throw new InvalidOperationException($"Process-system ({system}) already started");
        //     }
        //
        //     var asystem = new ActorSystem(system, cluster, appProfile, config);
        //     AddOrUpdateSystem(asystem);
        //
        //     try
        //     {
        //         asystem.Initialise();
        //
        //         // Set the default system if the 'default: yes' setting is in the ProcessSystemConfig
        //         defaultSystem = defaultSystem.IsValid
        //             ? (from c in config.Cluster
        //                where c.Default
        //                select system)
        //               .IfNone(defaultSystem)
        //             : system;
        //     }
        //     catch
        //     {
        //         systems = systems.Filter(a => a.SystemName != system).ToArray();
        //         try
        //         {
        //             asystem.Dispose();
        //         }
        //         catch { }
        //         throw;
        //     }
        // }
        
        public static Aff<RT, Unit> systemBootstrap =>
            from sys in ActorContextAff<RT>.LocalSystem
            from _ in ProcessEff.logInfo("Process system starting up")
            from rootParent in SuccessEff(new ActorItem(new NullProcess<RT>(sys.SystemName), new NullInbox(), ProcessFlags.Default))
            from rootProcess in new Actor<RT, Unit, Unit>(
                rootParent.Actor.Id[rootProcessName],
                cluster,
                parent,
                rootProcessName,
                SystemInbox,
                _ => this,
                ActorSystem.NoShutdown<ActorSystemBootstrap>(),
                null,
                Process.DefaultStrategy,
                ProcessFlags.Default,
                settings, 
                system
            )
        
            ;
        

            /*// Top tier
            system = ActorSystemAff<RT>.actorCreate<object>(root, Config.SystemProcessName, publish, null, ProcessFlags.Default);
            user   = ActorCreate<object>(root, Config.UserProcessName, publish, null, ProcessFlags.Default);
            js     = ActorCreate<ProcessId, RelayMsg>(root, "js", RelayActor.Inbox, () => System.User["process-hub-js"], null, ProcessFlags.Default);

            // Second tier
            sessionMonitor = ActorCreate<(SessionSync, Time), Unit>(system, Config.Sessions, SessionMonitor.Inbox, () => SessionMonitor.Setup(Sync, Settings.SessionTimeoutCheckFrequency), null, ProcessFlags.Default);
            deadLetters    = ActorCreate<DeadLetter>(system, Config.DeadLettersProcessName, publish, null, ProcessFlags.Default);
            errors         = ActorCreate<Exception>(system, Config.ErrorsProcessName, publish, null, ProcessFlags.Default);
            monitor        = ActorCreate<ClusterMonitor.State, ClusterMonitor.Msg>(system, Config.MonitorProcessName, ClusterMonitor.Inbox, () => ClusterMonitor.Setup(System), null, ProcessFlags.Default);

            Cluster.Iter(c =>
                         {
                             scheduler = ActorCreate<Scheduler.State, Scheduler.Msg>(system, Config.SchedulerName, Scheduler.Inbox, () => Scheduler.State.Empty, null, ProcessFlags.ListenRemoteAndLocal);
                         });

            inboxShutdown = ActorCreate<IActorInbox>(system, Config.InboxShutdownProcessName, inbox => inbox.Shutdown(), null, ProcessFlags.Default, 100000);

            reply = ask = ActorCreate<(long, Dictionary<long, AskActorReq>), object>(system, Config.AskProcessName, AskActor.Inbox, AskActor.Setup, null, ProcessFlags.ListenRemoteAndLocal);

            logInfo("Process system startup complete");

            return this;
        }*/

        public static Eff<RT, bool> InMessageLoop =>
            Eff<RT, bool>(e => e.EchoEnv.InMessageLoop);

        public static Aff<RT, Unit> stopAllSystems =>
            from sns in ActorContext.SystemNames
            from res in sns.Map(stopSystem).SequenceParallel()
            select unit;

        public static Aff<RT, Unit> stopSystem(SystemName system) =>
            from _1 in ActorContext.clearDefaultSystemIfMatch(system)
            from ct in SuccessEff(new ShutdownCancellationToken(system))
            from _2 in Process.onPreShutdown(ct).Match(identity, ignore) 
            from _3 in ct.Cancelled
                           ? unitEff
                           : from _4 in disposeSystem(system).Match(identity, ignore) 
                             from _5 in removeSystem(system)
                             select unit
            select unit;
        
        static Aff<RT, Unit> disposeSystem(SystemName system) =>
            from asystem in ActorContext.findSystem(system)
            from _       in asystem.Dispose<RT>()
            select unit;

        static Aff<RT, Unit> removeSystem(SystemName system) =>
            from _1 in ActorContext.removeSystem(system)
            from _2 in Process.onShutdown(system)
            select unit;

        public static Aff<RT, A> localSystem<A>(SystemName system, Aff<RT, A> ma) =>
            localAff<RT, RT, A>(env => env.LocalEchoEnv(env.EchoEnv.WithSystem(system)), ma);

        public static Eff<RT, A> localSystem<A>(SystemName system, Eff<RT, A> ma) =>
            localEff<RT, RT, A>(env => env.LocalEchoEnv(env.EchoEnv.WithSystem(system)), ma);

        public static Aff<RT, A> localSystem<A>(ProcessId pid, Aff<RT, A> ma) =>
            localSystem(pid.System, ma);

        public static Eff<RT, A> localSystem<A>(ProcessId pid, Eff<RT, A> ma) =>
            localSystem(pid.System, ma);

        public static Aff<RT, A> localSystem<A>(ActorSystem system, Aff<RT, A> ma) =>
            localAff<RT, RT, A>(env => env.LocalEchoEnv(env.EchoEnv.WithSystem(system.SystemName)), ma);

        public static Eff<RT, A> localSystem<A>(ActorSystem system, Eff<RT, A> ma) =>
            localEff<RT, RT, A>(env => env.LocalEchoEnv(env.EchoEnv.WithSystem(system.SystemName)), ma);

        public static Aff<RT, A> localContext<A>(ActorRequestContext requestContext, Aff<RT, A> ma) =>
            localAff<RT, RT, A>(env => env.LocalEchoEnv(env.EchoEnv.WithRequest(requestContext)), ma);

        public static Eff<RT, A> localContext<A>(ActorRequestContext requestContext, Eff<RT, A> ma) =>
            localEff<RT, RT, A>(env => env.LocalEchoEnv(env.EchoEnv.WithRequest(requestContext)), ma);

        /// <summary>
        /// Get the echo environment
        /// </summary>
        public static Eff<RT, EchoEnv> EchoEnv =>
            Eff<RT, EchoEnv>(env => env.EchoEnv);

        /// <summary>
        /// Get the system settings
        /// </summary>
        public static Eff<RT, ActorSystemConfig> GlobalSettings =
            SuccessEff(ActorSystemConfig.Default);

        /// <summary>
        /// Get the system settings
        /// </summary>
        public static Eff<RT, ProcessSystemConfig> LocalSettings =>
            EchoEnv.Bind(e => ActorContext.findSystem(e.SystemName).Map(s => s.Settings.Value));
        
        /// <summary>
        /// Get the local system from the Echo environment
        /// </summary>
        /// <typeparam name="RT"></typeparam>
        /// <returns></returns>
        public static Eff<RT, ActorSystem> LocalSystem =>
            EchoEnv.Bind(e => ActorContext.findSystem(e.SystemName)) | ActorContext.DefaultSystem;

        /// <summary>
        /// The 'user' process, which is the root for all processes spawned outside of an existing process
        /// </summary>
        static Eff<RT, ProcessId> User =>
            ActorContext.DefaultSystem.Map(sys => sys.User);

        /// <summary>
        /// The 'user' process, which is the root for all processes spawned outside of an existing process
        /// </summary>
        static Eff<RT, ActorItem> UserContext =>
            ActorContext.DefaultSystem.Map(sys => sys.UserContext.Self);

        public static Eff<RT, ProcessId> Self =>
            Eff<RT, ProcessId>(env =>  
                env.EchoEnv.InMessageLoop
                    ? env.EchoEnv.Request.IsSome
                        ? env.EchoEnv.Request.ValueUnsafe().Self.Actor.Id
                        : User.RunIO(env).IfFail(ProcessId.None)
                    : User.RunIO(env).IfFail(ProcessId.None));

        public static Eff<RT, ActorItem> SelfProcess =>
            from env in EchoEnv
            from res in env.InMessageLoop
                            ? env.Request.Match(
                                  Some: r  => SuccessEff(r.Self),
                                  None: UserContext)
                            : UserContext
            select res;

        public static Eff<RT, ProcessId> Parent =>
            Request.Map(r => r.Parent.Actor.Id);

        public static Eff<RT, ActorItem> ParentProcess =>
            Request.Map(r => r.Parent);

        public static Eff<RT, Seq<ProcessId>> Siblings =>
            SelfProcess.Map(a => a.Actor.Children.Values.Map(n => n.Actor.Id).Filter(x => x != a.Actor.Id).ToSeq());
        
        public static Eff<RT, ActorRequestContext> Request =>
            from e in EchoEnv
            from r in e.Request.Match(SuccessEff, FailEff<ActorRequestContext>(Error.New("Not in a message loop")))
            select r;

        public static Eff<RT, SessionId> SessionId =>
            from e in EchoEnv
            from r in e.SessionId.Match(SuccessEff, FailEff<SessionId>(Error.New("No active session")))
            select r;

        public static Aff<RT, A> localSessionId<A>(SessionId sessionId, Aff<RT, A> ma) =>
            localAff<RT, RT, A>(env => env.LocalEchoEnv(env.EchoEnv.WithSession(sessionId)), ma);

        public static Eff<RT, A> localSessionId<A>(SessionId sessionId, Eff<RT, A> ma) =>
            localEff<RT, RT, A>(env => env.LocalEchoEnv(env.EchoEnv.WithSession(sessionId)), ma);

        public static Aff<RT, Unit> publish(object message) =>
            from r in Request
            from _ in r.Self.Actor.Publish<RT>(message)
            select unit;

        public static Eff<RT, ProcessId> resolvePID(ProcessId pid) =>
            from env in EchoEnv
            from req in Request.Match(Succ: Some, Fail: _ => None)
            from res in pid.Path switch
                        {
                            "/__special__/self"          when req.IsNone => ActorContext.DefaultSystem.Map(s => s.User),
                            "/__special__/self"          when req.IsSome => Self,
                            "/__special__/sender"        when req.IsNone => SuccessEff(ProcessId.NoSender),
                            "/__special__/sender"        when req.IsSome => SuccessEff(req.ValueUnsafe().Sender),
                            "/__special__/parent"        when req.IsNone => ActorContext.DefaultSystem.Map(s => s.User),
                            "/__special__/parent"        when req.IsSome => SuccessEff(req.ValueUnsafe().Parent.Actor.Id),
                            "/__special__/user"                          => ActorContext.DefaultSystem.Map(s => s.User),
                            "/__special__/dead-letters"                  => ActorContext.system(env.SystemName).Map(s => s.DeadLetters),
                            "/__special__/root"                          => ActorContext.DefaultSystem.Map(s => s.Root),
                            "/__special__/errors"                        => ActorContext.DefaultSystem.Map(s => s.Errors),
                            _                                            => SuccessEff(pid)
                        }
            select res;
    }
}
