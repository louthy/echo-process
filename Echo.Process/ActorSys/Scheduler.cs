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

        public static State Inbox(State state, Msg msg)
        {
            try
            {
                state = ActorContext.System(Self).Cluster.Fold(state, (s, cluster) =>
                {
                    if (s.Scheduled.IsNone)
                    {
                        // Lazily load any items in the persistent store, once
                        s = new State(GetScheduled(cluster));
                    }

                    switch (msg)
                    {
                        case Msg.CheckMsg m: return Check(s, cluster);
                        case Msg.AddToScheduleMsg m: return AddToSchedule(s, m, cluster);
                        case Msg.RescheduleMsg m: return Reschedule(s, m, cluster);
                        case Msg.RemoveFromScheduleMsg m: return RemoveFromSchedule(s, m, cluster);
                        default: return s;
                    }
                });
            }
            catch (Exception e)
            {
                try
                {
                    logErr(e);
                }
                catch { }
            }

            if (msg is Msg.CheckMsg)
            {
                tellSelf(Msg.Check, schedule);
            }
            return state;
        }

        static State RemoveFromSchedule(State state, Msg.RemoveFromScheduleMsg msg, ICluster cluster)
        {
            cluster.DeleteHashField(msg.InboxKey, msg.Id);
            return state.Delete(msg.InboxKey, msg.Id);
        }

        static State AddToSchedule(State state, Msg.AddToScheduleMsg msg, ICluster cluster)
        {
            cluster.HashFieldAddOrUpdate(msg.InboxKey, msg.Id, msg.Message);
            return state.Add(msg.InboxKey, msg.Id, msg.Message);
        }

        static State Reschedule(State state, Msg.RescheduleMsg msg, ICluster cluster) =>
            cluster.GetHashField<RemoteMessageDTO>(msg.InboxKey, msg.Id).Map(dto =>
            {
                dto.Due = msg.When.Ticks;
                cluster.HashFieldAddOrUpdate(msg.InboxKey, msg.Id, dto);
                return state.Delete(msg.InboxKey, msg.Id).Add(msg.InboxKey, msg.Id, dto);
            })
            .IfNone(state);

        static State Check(State state, ICluster cluster)
        {
            var now = DateTime.UtcNow.Ticks;

            foreach (var map in state.Scheduled)
            {
                foreach (var outerKeyValue in map)
                {
                    foreach (var kv in outerKeyValue.Value)
                    {
                        if (kv.Value.Due < now)
                        {
                            var inboxKey = ActorInboxCommon.ClusterInboxKey(kv.Value.To, "user");
                            var inboxNotifyKey = ActorInboxCommon.ClusterInboxNotifyKey(kv.Value.To, "user");
                            if (cluster.Enqueue(inboxKey, kv.Value) > 0)
                            {
                                cluster.DeleteHashField(outerKeyValue.Key, kv.Key);
                                state = state.Delete(outerKeyValue.Key, kv.Key);
                                cluster.PublishToChannel(inboxNotifyKey, kv.Value.MessageId);
                            }
                        }
                    }
                }
            }
            return state;
        }

        static HashMap<string, HashMap<string, RemoteMessageDTO>> GetScheduled(ICluster cluster) =>
            cluster.QueryScheduleKeys(cluster.NodeName.Value)
                   .ToList()
                   .Fold(HashMap<string, HashMap<string, RemoteMessageDTO>>(), 
                    (s, key) =>
                        s.AddOrUpdate(key, cluster.GetHashFields<RemoteMessageDTO>(key)));

        public class Msg
        {
            public static Msg AddToSchedule(string inboxKey, string id, RemoteMessageDTO message) =>
                new AddToScheduleMsg(inboxKey, id, message);

            public static Msg Reschedule(string inboxKey, string id, DateTime when) =>
                new RescheduleMsg(inboxKey, id, when);

            public static Msg RemoveFromSchedule(string inboxKey, string id) =>
                new RemoveFromScheduleMsg(inboxKey, id);

            public static readonly Msg Check = 
                new CheckMsg();

            public class AddToScheduleMsg : Msg
            {
                public readonly string InboxKey;
                public readonly string Id;
                public readonly RemoteMessageDTO Message;
                public AddToScheduleMsg(string inboxKey, string id, RemoteMessageDTO message)
                {
                    InboxKey = inboxKey;
                    Id = id;
                    Message = message;
                }
            }

            public class RemoveFromScheduleMsg : Msg
            {
                public readonly string InboxKey;
                public readonly string Id;
                public RemoveFromScheduleMsg(string inboxKey, string id)
                {
                    InboxKey = inboxKey;
                    Id = id;
                }
            }

            public class RescheduleMsg : Msg
            {
                public readonly string InboxKey;
                public readonly string Id;
                public readonly DateTime When;
                public RescheduleMsg(string inboxKey, string id, DateTime when)
                {
                    InboxKey = inboxKey;
                    Id = id;
                    When = when;
                }
            }

            public class CheckMsg : Msg
            {
            }
        }

        public class State
        {
            public static readonly State Empty = new State(None);

            public readonly Option<HashMap<string, HashMap<string, RemoteMessageDTO>>> Scheduled;
            public State(Option<HashMap<string, HashMap<string, RemoteMessageDTO>>> scheduled)
            {
                Scheduled = scheduled;
            }

            public State Add(string key, string innerKey, RemoteMessageDTO msg) =>
                new State(Scheduled.Map(s => s.AddOrUpdate(key, innerKey, msg)));

            public State Delete(string key, string innerKey) =>
                new State(Scheduled.Map(s => s.Remove(key, innerKey)));
        }
    }
}
