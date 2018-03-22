using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;
using System;
using System.Linq;
using System.Threading;
using System.Reactive.Linq;

namespace Echo
{
    internal static class LocalScheduler
    {
        static object sync = new object();
        static Map<long, Seq<(string key, Func<object, Unit> action, object message, ActorRequestContext context, Option<SessionId> sessionId)>> actions;
        static Map<string, long> keys;
        static Que<(Schedule schedule, ProcessId pid, Func<object, Unit> action, object message, ActorRequestContext context, Option<SessionId> sessionId)> inbound;

        public static Unit Push(Schedule schedule, ProcessId pid, Func<object, Unit> action, object message)
        {
            if (schedule == Schedule.Immediate)
            {
                action(message);
                return unit;
            }
            else
            {
                if (schedule.Type == Schedule.PersistenceType.Persistent)
                {
                    throw new NotSupportedException("Persistent schedules are not supported for non-persistent processes");
                }
                else
                {
                    schedule = String.IsNullOrEmpty(schedule.Key)
                        ? schedule.SetKey(Guid.NewGuid().ToString())
                        : schedule;

                    var savedContext = ActorContext.Request;
                    var savedSession = ActorContext.SessionId;

                    lock (sync)
                    {
                        inbound = inbound.Enqueue((schedule, pid, action, message, savedContext, savedSession));
                    }
                    return unit;
                }
            }
        }

        public static IDisposable Run() =>
            Observable.Interval(TimeSpan.FromMilliseconds(1)).Subscribe(Process);

        static void Process(long time)
        {
            try
            {
                ProcessInboundQueue();
                ProcessActions();
            }
            catch(Exception e)
            {
                logErr(e);
            }
        }

        static void ProcessInboundQueue()
        {
            var now = DateTime.UtcNow.Ticks;
            while (inbound.Count > 0 && DateTime.UtcNow.Ticks - now < TimeSpan.TicksPerMillisecond)
            {
                var (schedule, pid, action, message, context, sessionId) = inbound.Peek();

                var key = pid.Path + "|" + schedule.Key;
                var existing = FindExistingScheduledMessage(key);

                RemoveExistingScheduledMessage(key);

                message = schedule.Fold(existing.Map(x => x.message).IfNoneUnsafe(schedule.Zero), message);

                actions = actions.AddOrUpdate(
                    schedule.Due.Ticks,
                    Some: seq => (key, action, message, context, sessionId).Cons(seq), 
                    None: () => Seq1((key, action, message, context, sessionId)));


                keys = keys.AddOrUpdate(key, schedule.Due.Ticks);

                lock (sync)
                {
                    inbound = inbound.Dequeue();
                }
            }
        }

        private static void RemoveExistingScheduledMessage(string key)
        {
            keys.Find(key).Match(
                Some: ticks =>
                {
                    actions = actions.TrySetItem(ticks, Some: seq => seq.Filter(tup => tup.key != key));
                },
                None: () => { });
        }

        private static Option<(string key, Func<object, Unit> action, object message, ActorRequestContext context, Option<SessionId> sessionId)> FindExistingScheduledMessage(string key) =>
            from ticks in keys.Find(key)
            from seq in actions.Find(ticks)
            from res in (from tup in seq
                         where tup.key == key
                         select tup).HeadOrNone()
            select res;

        static void ProcessActions()
        {
            var now = DateTime.UtcNow.Ticks;
            var ticksToProcess = actions.Keys.TakeWhile(t => t < now).ToList();

            foreach(var tick in ticksToProcess)
            {
                foreach(var item in actions[tick])
                {
                    try
                    {
                        if (item.context == null)
                        {
                            item.action(item.message);
                        }
                        else
                        {
                            ActorContext.System(item.context.Self.Actor.Id).WithContext(
                                         item.context.Self,
                                         item.context.Parent,
                                         item.context.Sender,
                                         item.context.CurrentRequest,
                                         item.context.CurrentMsg,
                                         item.sessionId,
                                         () =>
                                         {
                                             item.action(item.message);

                                             // Run the operations that affect the settings and sending of tells
                                             // in the order which they occured in the actor
                                             ActorContext.Request?.Ops?.Run();
                                         });
                        }
                    }
                    catch (Exception e)
                    {
                        logErr(e);
                    }
                    keys = keys.Remove(item.Item1);
                }
                actions = actions.Remove(tick);
            }
        }
    }
}
