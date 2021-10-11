using System;
using System.Threading;
using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;
using Echo;
using Echo.Config;
using Echo.Session;
using LanguageExt.UnitsOfMeasure;

namespace Echo
{
    internal record SystemActorState(
        ActorSystemConfig Config, 
        ProcessSystemConfig Settings, 
        SessionSync Sync, 
        IActorSystem System, 
        Option<ICluster> Cluster);
    
    internal record SystemActorMessage;
    
    internal static class SystemActor
    {
        public static SystemActorState Setup(
            CountdownEvent countdown,
            ActorSystemConfig config, 
            ProcessSystemConfig settings, 
            SessionSync sync, 
            IActorSystem system, 
            Option<ICluster> cluster,
            ClusterMonitor.State clusterState)
        {
            spawn<Exception>(config.ErrorsProcessName, static m => publish(m));
            spawn<DeadLetter>(config.DeadLettersProcessName, static m => publish(m));
            spawn<IActorInbox>(config.InboxShutdownProcessName, inbox => inbox.Shutdown());
            
            for (var i = 0; i < AskActor.Actors; i++)
            {
                spawn<AskActorState, object>($"{config.AskProcessName}-{i}", () => AskActor.Setup(countdown), AskActor.Inbox, ProcessFlags.ListenRemoteAndLocal);
            }

            if (cluster.Case is ICluster c)
            {
                spawn<Option<Scheduler.State>, Scheduler.Msg>(config.SchedulerName, Scheduler.Setup, Scheduler.Inbox, ProcessFlags.ListenRemoteAndLocal);
            }
            
            spawn<(SessionSync, Time), Unit>(config.Sessions, () => SessionMonitor.Setup(sync, settings.SessionTimeoutCheckFrequency), SessionMonitor.Inbox);
            spawn<ClusterMonitor.State, ClusterMonitor.Msg>(config.MonitorProcessName, () => ClusterMonitor.Setup(clusterState), ClusterMonitor.Inbox);
            
            return new SystemActorState(config, settings, sync, system, cluster);
        }


        public static SystemActorState Inbox(SystemActorState state, SystemActorMessage msg) =>
            state;
    }
}