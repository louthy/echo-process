using Echo;
using static Echo.Process;
using LanguageExt;
using static LanguageExt.Prelude;
using System.Linq;
using LanguageExt.ClassInstances;
using LanguageExt.TypeClasses;

namespace ScheduledMessages
{
    public enum NotificationType
    {
        Info,
        Warning,
        Error
    }

    // Simple record type for a notification
    public class Notification : Record<Notification>
    {
        public readonly NotificationType Type;
        public readonly string Message;

        public Notification(NotificationType type, string message)
        {
            Type = type;
            Message = message;
        }
    }

    // This is a struct implementation of Monoid for a Set of Notifications
    // It provides the initial Empty state and the functionality for doing the
    // associative Append operation.
    public struct MNotify : Monoid<Set<Notification>>
    {
        public Set<Notification> Append(Set<Notification> oldest, Set<Notification> newest) =>
            // Here you can process the notifications as they're sent
            // Or you could use a Lst to collect the items and process them in the Inbox
            oldest + newest; 

        public Set<Notification> Empty() =>
            Set<Notification>();
    }

    public static class Notifier
    {
        static ProcessId pid;

        public static void Startup() =>
            pid = spawn<Set<Notification>>("notify", Inbox);

        // NOTE: The use of the personId to shard the schedule and group notifications by person

        public static void Info(int personId, string message) =>
            tell(
                pid, 
                Set(new Notification(NotificationType.Info, message)), 
                Schedule.EphemeralAppend<MNotify, Set<Notification>>(1 * hour, $"person-{personId}"));

        public static void Warning(int personId, string message) =>
            tell(
                pid,
                Set(new Notification(NotificationType.Warning, message)),
                Schedule.EphemeralAppend<MNotify, Set<Notification>>(1 * hour, $"person-{personId}"));

        public static void Error(int personId, string message) =>
            tell(
                pid,
                Set(new Notification(NotificationType.Error, message)),
                Schedule.EphemeralAppend<MNotify, Set<Notification>>(1 * hour, $"person-{personId}"));

        public static void Inbox(Set<Notification> notifications)
        {
            // Send
        }
    }
}
