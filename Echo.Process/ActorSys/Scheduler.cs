using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;
using System;
using System.Linq;

namespace Echo
{
    internal static class Scheduler
    {
        static readonly Schedule schedule = Schedule.Ephemeral(TimeSpan.FromSeconds(0.1), "loop");

        // TODO: Serious optimisation needed if this is to scale

        public static void Inbox(Unit tick)
        {
            ActorContext.System(Self).Cluster.Iter(cluster =>
            {
                try
                {
                    var keys = cluster.QueryScheduleKeys(cluster.NodeName.Value).ToList();
                    var now = DateTime.UtcNow.Ticks;

                    foreach (var key in keys)
                    {
                        var items = cluster.GetHashFields<RemoteMessageDTO>(key);

                        foreach (var kv in items)
                        {
                            if (kv.Value.Due < now)
                            {
                                var inboxKey = ActorInboxCommon.ClusterInboxKey(kv.Value.To, "user");
                                var inboxNotifyKey = ActorInboxCommon.ClusterInboxNotifyKey(kv.Value.To, "user");
                                if (cluster.Enqueue(inboxKey, kv.Value) > 0)
                                {
                                    cluster.DeleteHashField(key, kv.Key);
                                    cluster.PublishToChannel(inboxNotifyKey, kv.Value.MessageId);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    try
                    {
                        logErr(e);
                    }
                    catch { }
                }
            });

            tellSelf(unit, schedule);
        }
    }
}
