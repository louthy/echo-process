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
    internal class ActorSystemBootstrap2
    {
        public static (IActor RootActor, ILocalActorInbox RootInbox, ActorItem Parent) Boot(
            CountdownEvent countdown,
            IActorSystem system, 
            Option<ICluster> cluster, 
            ProcessId rootId, 
            ProcessName rootProcessName, 
            ActorSystemConfig config, 
            ProcessSystemConfig settings, 
            SessionSync sync,
            ClusterMonitor.State clusterState)
        {
            var parent = new ActorItem(new NullProcess(system.Name), new NullInbox(), ProcessFlags.Default);

            var rootActor = new Actor<RootActorState, RootActorMessage>(
                cluster,
                parent,
                rootProcessName,
                (s, m) => RootActor.Inbox(s, m).AsValueTask(),
                _ => RootActor.Setup(countdown, config, settings, sync, system, cluster, clusterState).AsValueTask(),
                null,
                null,
                Process.DefaultStrategy,
                ProcessFlags.Default,
                settings,
                system);
            
            var rootInbox = new ActorInboxLocal<RootActorState, RootActorMessage>();

            return (rootActor, rootInbox, parent);
        }
    }
}