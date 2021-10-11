using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;
using System;
using System.Linq;

namespace Echo
{
    /// <summary>
    /// Persistent scheduler
    /// </summary>
    internal static class Scheduler
    {
        static readonly Schedule schedule = Schedule.Ephemeral(TimeSpan.FromSeconds(0.1), "loop");

        public static Option<State> Setup()
        {
            tellSelf(Msg.Check, schedule);
            return None;
        }

        public static Option<State> Inbox(Option<State> state, Msg msg)
        {
            var cluster = (ICluster) ActorContext.System(Self).Cluster.Case;
            if (cluster == null) return state;

            var nstate = LoadScheduledIfNone(state, cluster);

            return msg switch
                   {
                       Msg.CheckMsg m              => Check(nstate, cluster),
                       Msg.AddToScheduleMsg m      => AddToSchedule(nstate, m, cluster),
                       Msg.RescheduleMsg m         => Reschedule(nstate, m, cluster),
                       Msg.RemoveFromScheduleMsg m => RemoveFromSchedule(nstate, m, cluster),
                       _                           => nstate
                   };
        }

        static State RemoveFromSchedule(State state, Msg.RemoveFromScheduleMsg msg, ICluster cluster)
        {
            var pkey  = MakePersistentKey(cluster);
            var field = $"{msg.InboxKey}::{msg.Id}";
 
            cluster.DeleteHashField(pkey, field);
            return state.Delete(msg.InboxKey, msg.Id);
        }

        static State AddToSchedule(State state, Msg.AddToScheduleMsg msg, ICluster cluster)
        {
            var pkey  = MakePersistentKey(cluster);
            var field = $"{msg.InboxKey}::{msg.Id}";
 
            cluster.HashFieldAddOrUpdate(pkey, field, msg.Message);
            return state.Add(msg.InboxKey, msg.Id, msg.Message);
        }

        static State Reschedule(State state, Msg.RescheduleMsg msg, ICluster cluster)
        {
            var pkey  = MakePersistentKey(cluster);
            var field = $"{msg.InboxKey}::{msg.Id}";

            return cluster.GetHashField<RemoteMessageDTO>(pkey, field)
                          .Map(dto => {
                                   dto.Due = msg.When.Ticks;
                                   cluster.HashFieldAddOrUpdate(pkey, field, dto);
                                   return state.Delete(msg.InboxKey, msg.Id).Add(msg.InboxKey, msg.Id, dto);
                               })
                          .IfNone(state);
        }

        static State Check(State state, ICluster cluster)
        {
            var now = DateTime.UtcNow.Ticks;

            foreach (var inbox in state.Scheduled)
            {
                foreach (var (key, value) in inbox.Value)
                {
                    if (value.Due < now)
                    {
                        var inboxKey       = ActorInboxCommon.ClusterInboxKey(value.To, "user");
                        var inboxNotifyKey = ActorInboxCommon.ClusterInboxNotifyKey(value.To, "user");
                        
                        // Enqueue the scheduled item in the process' message queue
                        if (cluster.Enqueue(inboxKey, value) > 0)
                        {
                            var pkey  = MakePersistentKey(cluster);
                            var field = $"{inbox.Key}::{key}";

                            // Wipe the scheduled item from Redis
                            cluster.DeleteHashField(pkey, field);
                            
                            // Wipe the scheduled item from memory
                            state = state.Delete(inbox.Key, key);
                            
                            // Notify the process that it has a new message
                            cluster.PublishToChannel(inboxNotifyKey, value.MessageId);
                        }
                    }
                }
            }
            
            // We should always keep checking
            tellSelf(Msg.Check, schedule);

            return state;
        }

        static Option<(string InboxKey, string Key)> GetParts(string key)
        {
            var ix = key.IndexOf("::", StringComparison.InvariantCulture);
            if (ix == -1) return None;
            var inboxKey = key.Substring(0, ix);
            key = key.Substring(ix + 2);
            return (inboxKey, key);
        }

        static State LoadScheduledIfNone(Option<State> state, ICluster cluster) =>
            state.Case switch
            {
                State s => s,
                _       => GetScheduled(cluster)
            };

        static string MakePersistentKey(ICluster cluster) =>
            $"/__schedule/{cluster.NodeName.Value}";

        static State GetScheduled(ICluster cluster) =>
            new State(cluster.GetHashFields<RemoteMessageDTO>(MakePersistentKey(cluster))
                             .AsEnumerable()
                             .Sequence(static p => GetParts(p.Key).Map(np => (np.InboxKey, np.Key, p.Value)))
                             .Map(static ps => ps.ToSeq())
                             .IfNone(Empty)
                             .Fold(HashMap<string, HashMap<string, RemoteMessageDTO>>(),
                                   (s, triple) => s.AddOrUpdate(triple.InboxKey, triple.Key, triple.Value)));
 
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

        public record State(HashMap<string, HashMap<string, RemoteMessageDTO>> Scheduled)
        {
            public static readonly State Empty = new State(HashMap<string, HashMap<string, RemoteMessageDTO>>());

            public State Add(string inboxKey, string key, RemoteMessageDTO msg) =>
                this with { Scheduled = Scheduled.AddOrUpdate(inboxKey, key, msg)};

            public State Delete(string inboxKey, string key) =>
                this with { Scheduled = Scheduled.Remove(inboxKey, key) };
        }
    }
}
