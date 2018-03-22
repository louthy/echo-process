using LanguageExt;
using System;

namespace Echo
{
    public class Schedule : Record<Schedule>
    {
        public enum PersistenceType
        {
            Persistent,
            Ephemeral
        }

        public readonly DateTime Due;
        public readonly PersistenceType Type;
        public readonly string Key;

        Schedule(DateTime due, PersistenceType type, string key)
        {
            Due = due;
            Type = type;
            Key = key;
        }

        public Schedule SetKey(string key) =>
            new Schedule(Due, Type, key);

        public Schedule MakeEphemeral() =>
            new Schedule(Due, PersistenceType.Ephemeral, Key);

        public Schedule MakePersistent() =>
            new Schedule(Due, PersistenceType.Persistent, Key);

        public readonly static Schedule Immediate = new Schedule(DateTime.MinValue, PersistenceType.Ephemeral, null);
        public static Schedule Persistent(DateTime due) => new Schedule(due, PersistenceType.Persistent, null);
        public static Schedule Ephemeral(DateTime due) => new Schedule(due, PersistenceType.Ephemeral, null);
        public static Schedule Persistent(TimeSpan due) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Persistent, null);
        public static Schedule Ephemeral(TimeSpan due) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Ephemeral, null);
        public static Schedule Persistent(DateTime due, string key) => new Schedule(due, PersistenceType.Persistent, key);
        public static Schedule Ephemeral(DateTime due, string key) => new Schedule(due, PersistenceType.Ephemeral, key);
        public static Schedule Persistent(TimeSpan due, string key) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Persistent, key);
        public static Schedule Ephemeral(TimeSpan due, string key) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Ephemeral, key);
    }
}
