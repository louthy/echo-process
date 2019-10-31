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

        [NonRecord]
        public readonly Func<object, object, object> Fold;

        [NonRecord]
        public readonly object Zero;

        protected Schedule(DateTime due, PersistenceType type, string key, Func<object, object, object> fold, object zero)
        {
            Due = due;
            Type = type;
            Key = key;
            Fold = fold;
            Zero = zero;
        }

        public Schedule SetDue(DateTime due) =>
            new Schedule(due, Type, Key, Fold, Zero);

        public Schedule SetKey(string key) =>
            new Schedule(Due, Type, key, Fold, Zero);

        public Schedule MakeEphemeral() =>
            new Schedule(Due, PersistenceType.Ephemeral, Key, Fold, Zero);

        public Schedule MakePersistent() =>
            new Schedule(Due, PersistenceType.Persistent, Key, Fold, Zero);

        public readonly static Schedule Immediate = new Schedule(DateTime.MinValue, PersistenceType.Ephemeral, null, TakeLatest, default(object));
        public static Schedule Persistent(DateTime due) => new Schedule(due, PersistenceType.Persistent, null, TakeLatest, default(object));
        public static Schedule Ephemeral(DateTime due) => new Schedule(due, PersistenceType.Ephemeral, null, TakeLatest, default(object));
        public static Schedule Persistent(TimeSpan due) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Persistent, null, TakeLatest, default(object));
        public static Schedule Ephemeral(TimeSpan due) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Ephemeral, null, TakeLatest, default(object));
        public static Schedule Persistent(DateTime due, string key) => new Schedule(due, PersistenceType.Persistent, key, TakeLatest, default(object));
        public static Schedule Ephemeral(DateTime due, string key) => new Schedule(due, PersistenceType.Ephemeral, key, TakeLatest, default(object));
        public static Schedule Persistent(TimeSpan due, string key) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Persistent, key, TakeLatest, default(object));
        public static Schedule Ephemeral(TimeSpan due, string key) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Ephemeral, key, TakeLatest, default(object));

        public static Schedule PersistentAppend<MonoidA, A>(DateTime due, string key) where MonoidA : struct, Monoid<A> => 
            PersistentFold<A, A>(due, key, default(MonoidA).Empty(), default(MonoidA).Append);

        public static Schedule EphemeralAppend<MonoidA, A>(DateTime due, string key) where MonoidA : struct, Monoid<A> => 
            EphemeralFold<A, A>(due, key, default(MonoidA).Empty(), default(MonoidA).Append);

        public static Schedule PersistentAppend<MonoidA, A>(TimeSpan due, string key) where MonoidA : struct, Monoid<A> => 
            PersistentFold<A, A>(due, key, default(MonoidA).Empty(), default(MonoidA).Append);

        public static Schedule EphemeralAppend<MonoidA, A>(TimeSpan due, string key) where MonoidA : struct, Monoid<A> => 
            EphemeralFold<A, A>(due, key, default(MonoidA).Empty(), default(MonoidA).Append);

        public static Schedule PersistentFold<S, A>(DateTime due, string key, S state, Func<S, A, S> f) => new Schedule(due, PersistenceType.Persistent, key, Wrap(f), state);
        public static Schedule EphemeralFold<S, A>(DateTime due, string key, S state, Func<S, A, S> f) => new Schedule(due, PersistenceType.Ephemeral, key, Wrap(f), state);
        public static Schedule PersistentFold<S, A>(TimeSpan due, string key, S state, Func<S, A, S> f) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Persistent, key, Wrap(f), state);
        public static Schedule EphemeralFold<S, A>(TimeSpan due, string key, S state, Func<S, A, S> f) => new Schedule(DateTime.UtcNow.Add(due), PersistenceType.Ephemeral, key, Wrap(f), state);

        static object TakeLatest(object a, object b) => b;
        static Func<object, object, object> Wrap<S, A>(Func<S, A, S> f) => (object a, object b) => f((S)a, (A)b);
        static Func<object, object, object> Wrap<A>(Func<A, A, A> f) => (object a, object b) => f((A)a, (A)b);
    }
}
