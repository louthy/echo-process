using LanguageExt;
using LanguageExt.ClassInstances;
using LanguageExt.TypeClasses;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Echo
{
    public class Schedule : Record<Schedule>
    {
        public enum PersistenceType : byte
        {
            Persistent,
            Ephemeral
        }

        public readonly DateTime Due;
        public readonly string Key;
        public readonly PersistenceType Type;
        public readonly Func<object, object, object> Fold;

        protected Schedule(DateTime due, PersistenceType type, string key, Func<object, object, object> fold)
        {
            Due = due;
            Type = type;
            Key = key;
            Fold = fold;
        }

        public Schedule SetKey(string key) =>
            new Schedule(Due, Type, key, Fold);

        public Schedule MakeEphemeral() =>
            new Schedule(Due, PersistenceType.Ephemeral, Key, Fold);

        public Schedule MakePersistent() =>
            new Schedule(Due, PersistenceType.Persistent, Key, Fold);

        public readonly static Schedule Immediate = new Schedule(DateTime.MinValue, PersistenceType.Ephemeral, null, TakeLatest);
        public static Schedule Persistent(DateTime due) => new Schedule(due, PersistenceType.Persistent, null, TakeLatest);
        public static Schedule Ephemeral(DateTime due) => new Schedule(due, PersistenceType.Ephemeral, null, TakeLatest);
        public static Schedule Persistent(TimeSpan due) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Persistent, null, TakeLatest);
        public static Schedule Ephemeral(TimeSpan due) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Ephemeral, null, TakeLatest);
        public static Schedule Persistent(DateTime due, string key) => new Schedule(due, PersistenceType.Persistent, key, TakeLatest);
        public static Schedule Ephemeral(DateTime due, string key) => new Schedule(due, PersistenceType.Ephemeral, key, TakeLatest);
        public static Schedule Persistent(TimeSpan due, string key) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Persistent, key, TakeLatest);
        public static Schedule Ephemeral(TimeSpan due, string key) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Ephemeral, key, TakeLatest);

        public static Schedule PersistentAppend<SemigroupA, A>(DateTime due, string key) where SemigroupA : struct, Monoid<A> => 
            PersistentFold<A>(due, key, default(SemigroupA).Append);

        public static Schedule EphemeralAppend<SemigroupA, A>(DateTime due, string key) where SemigroupA : struct, Monoid<A> => 
            EphemeralFold<A>(due, key, default(SemigroupA).Append);

        public static Schedule PersistentAppend<SemigroupA, A>(TimeSpan due, string key) where SemigroupA : struct, Monoid<A> => 
            PersistentFold<A>(due, key, default(SemigroupA).Append);

        public static Schedule EphemeralAppend<SemigroupA, A>(TimeSpan due, string key) where SemigroupA : struct, Monoid<A> => 
            EphemeralFold<A>(due, key, default(SemigroupA).Append);

        public static Schedule PersistentFold<A>(DateTime due, string key, Func<A, A, A> f) => new Schedule(due, PersistenceType.Persistent, key, Wrap(f));
        public static Schedule EphemeralFold<A>(DateTime due, string key, Func<A, A, A> f) => new Schedule(due, PersistenceType.Ephemeral, key, Wrap(f));
        public static Schedule PersistentFold<A>(TimeSpan due, string key, Func<A, A, A> f) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Persistent, key, Wrap(f));
        public static Schedule EphemeralFold<A>(TimeSpan due, string key, Func<A, A, A> f) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Ephemeral, key, Wrap(f));

        static object TakeLatest(object a, object b) => b;
        static Func<object, object, object> Wrap<A>(Func<A, A, A> f) =>
            (object a, object b) => f((A)a, (A)b);
    }
}
