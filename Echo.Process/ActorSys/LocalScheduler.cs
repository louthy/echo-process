using System;
using System.Threading;
using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;

namespace Echo
{
    /// <summary>
    /// Local in-memory scheduler
    /// </summary>
    internal static class LocalScheduler
    {
        static readonly AtomHashMap<string, IDisposable> scheduled = AtomHashMap<string, IDisposable>();  
        
        /// <summary>
        /// Push an item onto the local schedule.  Schedule.Immediate items will be run synchronously
        /// </summary>
        /// <param name="schedule">Schedule</param>
        /// <param name="pid">Process that's scheduling an action</param>
        /// <param name="action">The action to run</param>
        /// <param name="message">The message to pass to the action</param>
        /// <exception cref="NotSupportedException">Schedule.Persistent not supported locally</exception>
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
                    var due = (long)(schedule.Due.ToUniversalTime() - DateTime.UtcNow).TotalMilliseconds;
                    if (due < 1)
                    {
                        action(message);
                        return unit;
                    }

                    schedule = String.IsNullOrEmpty(schedule.Key)
                                   ? schedule.SetKey(Guid.NewGuid().ToString())
                                   : schedule;

                    scheduled.AddOrUpdate(MakeKey(pid, schedule),
                                          Some: exists => {
                                                    exists.Dispose();
                                                    return StartTimer(MakeKey(pid, schedule), action, message, due);
                                                },
                                          None: () => StartTimer(MakeKey(pid, schedule), action, message, due));
                    return unit;
                }
            }
        }

        /// <summary>
        /// Cancel a scheduled message
        /// </summary>
        public static Unit Cancel(ProcessId pid, string key) =>
            scheduled.Swap(sch => {
                               var skey = MakeKey(pid, key);
                               if (sch.Find(skey).Case is IDisposable d) d.Dispose();
                               return sch.Remove(skey);
                           });

        /// <summary>
        /// Shutdown
        /// </summary>
        /// <returns></returns>
        public static Unit Shutdown() =>
            scheduled.Swap(schs => {
                               schs.Iter(s => s.Dispose());
                               return Empty;
                            });

        /// <summary>
        /// Start the scheduled item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="action"></param>
        /// <param name="message"></param>
        /// <param name="due"></param>
        static IDisposable StartTimer(string key, Func<object, Unit> action, object message, long due)
        {
            var savedContext        = ActorContext.Request;
            var savedSession        = ActorContext.SessionId;
            var savedConversationId = ActorContext.ConversationId;
            
            void onEvent(object _)
            {
                scheduled.Remove(key);
                RunAction(savedContext, savedSession, savedConversationId, action, message);
            }

            return new Timer(onEvent, null, due, -1);
        }

        /// <summary>
        /// Run the scheduled action in its original context
        /// </summary>
        static Unit RunAction(ActorRequestContext context, Option<SessionId> sessionId, long savedConversationId, Func<object, Unit> action, object message)
        {
            try
            {
                return context == null
                           ? action(message)
                           : ActorContext.System(context.Self.Actor.Id).WithContext(
                               context.Self,
                               context.Parent,
                               context.Sender,
                               context.CurrentRequest,
                               context.CurrentMsg,
                               sessionId,
                               savedConversationId,
                               () => {
                                   action(message);
                                   return unit;
                               });
            }
            catch (Exception e)
            {
                logErr(e);
                return default;
            }
        }

        /// <summary>
        /// Make a namespaced key
        /// </summary>
        static string MakeKey(ProcessId pid, Schedule schedule) =>
            MakeKey(pid, schedule.Key);

        /// <summary>
        /// Make a namespaced key
        /// </summary>
        static string MakeKey(ProcessId pid, string key) =>
            $"{pid.Path}|{key}";
    }
}
