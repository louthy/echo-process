using System;
using System.Threading;
using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;
using Echo;
using Echo.Config;
using Echo.Session;

namespace Echo
{
    internal static class RootActor
    {
        public static RootActorState Setup(
            CountdownEvent countdown,
            ActorSystemConfig config, 
            ProcessSystemConfig settings, 
            SessionSync sync, 
            IActorSystem system, 
            Option<ICluster> cluster,
            ClusterMonitor.State clusterState) =>
            Startup(new RootActorStateNotRunning(countdown, config, settings, sync, system, cluster, clusterState));

        public static RootActorState Inbox(RootActorState state, RootActorMessage msg) =>
            (state, msg) switch
            {
                (RootActorStateNotRunning, RootActorStartUp) => Startup(state),
                _ => state
            };

        static RootActorState Startup(RootActorState state)
        {
            var processHubJs = Self.Child(state.Config.UserProcessName).Child("process-hub-js");
            
            // Create the user root
            spawn<object>(state.Config.UserProcessName, _ => { });
            
            // Create the system root
            spawn<SystemActorState, SystemActorMessage>(
                state.Config.SystemProcessName, 
                () => SystemActor.Setup(state.Countdown, state.Config, state.Settings, state.Sync, state.System, state.Cluster, state.ClusterState), 
                SystemActor.Inbox);
            
            // Create the JS relay
            spawn<ProcessId, RelayMsg>("js", () => processHubJs, RelayActor.Inbox, Lazy: true);
            
            return new RootActorStateRunning(state.Countdown, state.Config, state.Settings, state.Sync, state.System, state.Cluster, state.ClusterState);
        }
    }
    
    internal abstract record RootActorState(
        CountdownEvent Countdown,
        ActorSystemConfig Config, 
        ProcessSystemConfig Settings, 
        SessionSync Sync, 
        IActorSystem System, 
        Option<ICluster> Cluster,
        ClusterMonitor.State ClusterState);
    
    internal record RootActorStateNotRunning(
        CountdownEvent Countdown,
        ActorSystemConfig Config, 
        ProcessSystemConfig Settings, 
        SessionSync Sync, 
        IActorSystem System, 
        Option<ICluster> Cluster,
        ClusterMonitor.State ClusterState) : RootActorState(Countdown, Config, Settings, Sync, System, Cluster, ClusterState);
    
    internal record RootActorStateRunning(
        CountdownEvent Countdown,
        ActorSystemConfig Config, 
        ProcessSystemConfig Settings, 
        SessionSync Sync, 
        IActorSystem System, 
        Option<ICluster> Cluster,
        ClusterMonitor.State ClusterState) : RootActorState(Countdown, Config, Settings, Sync, System, Cluster, ClusterState);

    internal abstract record RootActorMessage;
    internal record RootActorStartUp : RootActorMessage;
}