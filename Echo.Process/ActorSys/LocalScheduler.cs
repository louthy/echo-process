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
        static Map<long, Seq<(string key, Action action, ActorRequestContext context, Option<SessionId> sessionId)>> actions;
        static Map<string, long> keys;
        static Que<(Schedule schedule, ProcessId pid, Action action, ActorRequestContext context, Option<SessionId> sessionId)> inbound;

        public static Unit Push(Schedule schedule, ProcessId pid, Action action)
        {
            if (schedule == Schedule.Immediate)
            {
                action();
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
                        inbound = inbound.Enqueue((schedule, pid, action, savedContext, savedSession));
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
                var (schedule, pid, action, context, sessionId) = inbound.Peek();

                var key = pid.Path + "|" + schedule.Key;

                keys.Find(key).Match(
                    Some: ticks =>
                    {
                        actions = actions.TrySetItem(ticks, Some: seq => seq.Filter(tup => tup.key != key));
                    },
                    None: () => { });

                actions = actions.AddOrUpdate(schedule.Due.Ticks, Some: seq => (key, action, context, sessionId).Cons(seq), None: () => Seq1((key, action, context, sessionId)));
                keys = keys.AddOrUpdate(key, schedule.Due.Ticks);

                lock (sync)
                {
                    inbound = inbound.Dequeue();
                }
            }
        }

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
                            item.action();
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
                                             item.action();

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
