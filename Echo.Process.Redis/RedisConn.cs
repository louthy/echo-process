using System;
using System.Reactive.Subjects;
using System.Threading;
using LanguageExt;
using static LanguageExt.Prelude;
using LanguageExt.Interfaces;
using StackExchange.Redis;

namespace Echo
{
    /// <summary>
    /// Represents a connection to Redis and any subscriptions
    /// </summary>
    public class RedisConn : IDisposable
    {
        internal int databaseNumber;
        internal ConnectionMultiplexer redis;
        internal Atom<HashMap<string, Subject<RedisValue>>> subscriptions = Atom(HashMap<string, Subject<RedisValue>>());

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="databaseNumber">Redis DB number</param>
        /// <param name="redis">Redis connection</param>
        internal RedisConn(int databaseNumber, ConnectionMultiplexer redis)
        {
            this.databaseNumber = databaseNumber;
            this.redis = redis;
        }

        /// <summary>
        /// Shutdown
        /// </summary>
        public void Dispose()
        {
            var r = Interlocked.Exchange(ref redis, null);
            if (r != null)
            {
                try
                {
                    subscriptions.Swap(subs => { 
                        subs.Iter(v => v.Dispose());
                        return Empty;
                    });
                }
                catch 
                {
                    // TODO: Consider how to log errors here
                }

                try
                {
                    if (r.IsConnected)
                    {
                        r.Close();
                    }
                }
                catch 
                { 
                    // TODO: Consider how to log errors here
                }

                r.Dispose();
            }
        }
    }
}