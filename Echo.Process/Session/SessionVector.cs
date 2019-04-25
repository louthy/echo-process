using LanguageExt;
using System;
using static LanguageExt.Prelude;

namespace Echo.Session
{
    /// <summary>
    /// Version vector conflict strategy
    /// </summary>
    public enum VectorConflictStrategy
    {
        /// <summary>
        /// Take the first item
        /// </summary>
        First,

        /// <summary>
        /// Take the last item
        /// </summary>
        Last,

        /// <summary>
        /// Keep all items of the same time
        /// </summary>
        Branch
    }

    /// <summary>
    /// Simple version vector.  There can be multiple values stored for the
    /// same event. The implementation will be replaced with Dotted Version 
    /// Vectors once I have implemented a general system for it in the Core.
    /// </summary>
    public class ValueVector
    {
        public readonly long Time;
        public readonly Seq<object> Vector;

        public ValueVector(long time, Seq<object> root)
        {
            Time = time;
            Vector = root;
        }

        public ValueVector AddValue(long time, object value, VectorConflictStrategy strategy)
        {
            if(Vector.Count == 0 || time > Time)
            {
                return new ValueVector(time, Seq1(value));
            }

            if( time < Time)
            {
                // A value from the past has arrived, we're going to drop it because
                // we've already moved on.
                return this;
            }

            if (Vector.Exists(x => x.Equals(value)))
            {
                // There's already an entry at the same time with the
                // same value
                return this;
            }
            else
            {
                // Conflict!
                switch(strategy)
                {
                    case VectorConflictStrategy.First:  return this;
                    case VectorConflictStrategy.Last:   return new ValueVector(time, Seq1(value));
                    case VectorConflictStrategy.Branch: return new ValueVector(Time, Vector.Add(value));
                    default: throw new ArgumentException("VectorConflictStrategy not supported: " + strategy);
                }
            }
        }
    }

    public class SessionVector
    {
        public readonly int TimeoutSeconds;

        /// <summary>
        /// only stores the data that the particular node is interested in. 
        /// Unit if the node is interested in data but does not have a value yet.
        /// </summary>
        Map<string, Either<Unit, ValueVector>> data;
        DateTime lastAccess;
        DateTime expires;
        object sync = new object();

        public static SessionVector Create(int timeout, VectorConflictStrategy strategy) =>
            new SessionVector(DateTime.UtcNow, timeout);

        /// <summary>
        /// Ctor
        /// </summary>
        private SessionVector(DateTime lastAccess, int timeoutSeconds)
        {
            this.lastAccess = lastAccess;
            TimeoutSeconds = timeoutSeconds;
            this.data = Map<string, Either<Unit, ValueVector>>();
        }

        /// <summary>
        /// Key/value store for the session
        /// only stores the data that  the particular node is interested in. 
        /// Unit if the node is interested in data but does not have a value yet.        
        /// </summary>
        public Map<string, Either<Unit, ValueVector>> Data => data;

        /// <summary>
        /// UTC date of last access
        /// </summary>
        public DateTime LastAccess => lastAccess;

        /// <summary>
        /// The date-time of expiry
        /// </summary>
        public DateTime Expires => expires;

        /// <summary>
        /// Invoke to keep the session alive
        /// </summary>
        public void Touch()
        {
            lastAccess = DateTime.UtcNow;
            expires = lastAccess.AddSeconds(TimeoutSeconds);
        }

        /// <summary>
        /// Remove a key from the session key/value store
        /// </summary>
        public void ClearKeyValue(long vector, string key)
        {
            lock (sync)
            {
                data = data.Remove(key);
            }
            Touch();
        }

        /// <summary>
        /// Add or update a key in the session key/value store
        /// </summary>
        public void SetKeyValue(long time, string key, object value, VectorConflictStrategy strategy)
        {
            lock (sync)
            {
                data = (from d      in data.Find(key)
                        from vector in d.ToOption()
                        select data.AddOrUpdate(key, vector.AddValue(time, value, strategy)))
                       .IfNone(()  => data.AddOrUpdate(key, new ValueVector(time, Seq1(value))));
            }
            Touch();
        }

        /// <summary>
        /// Checks local cache for a session data key. If does not exists uses get (redis) to
        /// retrieve the data. If data does not exist, an entry is still added to local cache as Unit (Left)
        /// to allow syncing with other published data update messages later.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="get"></param>
        /// <returns></returns>
        public Option<ValueVector> ProvideData(string key, Func<Option<ValueVector>> get)
        {
            Touch();

            lock (sync)
            {
                return data.Find(key).IfNone(() =>
                        get().Match<Either<Unit, ValueVector>>(
                            Some: v =>
                            {
                                data = data.AddOrUpdate(key, v);
                                return v;
                            },
                            None: () =>
                            {
                                data = data.AddOrUpdate(key, unit);
                                return unit;
                            })).ToOption();
            }
        }
    }
}
