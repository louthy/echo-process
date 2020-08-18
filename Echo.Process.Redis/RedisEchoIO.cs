using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LanguageExt;
using Newtonsoft.Json;
using System.Threading;
using System.Reflection;
using StackExchange.Redis;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Reactive.Subjects;
using System.Runtime.Serialization;
using Echo.Client;
using LanguageExt.UnsafeValueAccess;
using static LanguageExt.Prelude;

namespace Echo
{
    /// <summary>
    /// Implementation of the EchoIO interface.
    /// Allows the core Echo system to use Redis for persistent messaging
    /// </summary>
    public struct RedisEchoIO : EchoIO
    {
        // TODO: These facts exist elsewhere - normalise
        const string userInboxSuffix = "-user-inbox";
        const string metaDataSuffix  = "-metadata";
        const string regdPrefix      = "/__registered/";
        const int TimeoutRetries = 5;

        /// <summary>
        /// Internal state 
        /// </summary>
        readonly IntState state;

        /// <summary>
        /// Ctor
        /// </summary>
        public RedisEchoIO(ClusterConfig config, SerialiseIO serialiseIO) =>
            state = new IntState(config, serialiseIO);
        
        /// <summary>
        /// Get the internal state
        /// </summary>
        IntState State => state.Config == null
            ? throw new InvalidOperationException("RedisEchoIO hasn't been initialised through the constructor")
            : state;
        
        /// <summary>
        /// Thread safe connection
        /// </summary>
        public async ValueTask<bool> ClusterConnect()
        {
            var s = State;
            
            return await s.Config.Connection.SwapAsync(async conn => conn.Case switch { 
                NoneCase<IDisposable> _       => await Connect(),
                SomeCase<IDisposable> (var c) => Some(c),
                _                             => None
            });

            async ValueTask<Option<IDisposable>> Connect()
            {
                var dbNumber = parseUInt(s.Config.CatalogueName).IfNone(() => raise<uint>(new ArgumentException("Parsing CatalogueName as a number that is 0 or greater, failed.")));
                var redis = await RetryAsync(default, async _ => await ConnectionMultiplexer.ConnectAsync(s.Config.ConnectionString));
                var conn = new RedisConn((int) dbNumber, redis);
                return Some(conn as IDisposable);
            }
        }

        /// <summary>
        /// Thread-safe disconnection 
        /// </summary>
        public async ValueTask<bool> ClusterDisconnect()
        {
            return await State.Config.Connection.SwapAsync(async conn => conn.Case switch {
                SomeCase<IDisposable> (var c) => await Disconnect(c as RedisConn),
                _                             => None
            });
            
            async ValueTask<Option<IDisposable>> Disconnect(RedisConn conn)
            {
                await conn.redis.CloseAsync(true);
                conn.redis.Dispose();
                return None;
            }
        }

        /// <summary>
        /// Publish to a message to a channel
        /// </summary>
        public ValueTask<int> PublishToChannel<A>(string channelName, A data) =>
            RetryAsync(State, async s => (int) await s.Conn.GetSubscriber().PublishAsync(
                $"{channelName}{s.Config.CatalogueName}",
                Serialise(data, s)));

        /// <summary>
        /// Turn a channel into an Rx subject that we can subscribe to
        /// </summary>
        async ValueTask<Subject<RedisValue>> GetSubject(string channelName)
        {
            var s = State;
 
            channelName += State.Config.CatalogueName;
            Subject<RedisValue> subject = null; 

            await State.Subs.SwapAsync(async subs => {

                if (subs.ContainsKey(channelName))
                {
                    subject = subs[channelName]; 
                    return subs;
                }
                else
                {
                    subject = new Subject<RedisValue>();
                    subs = subs.Add(channelName, subject);

                    await RetryAsync(s, async st => await st.Conn.GetSubscriber().SubscribeAsync(
                        channelName,
                        (channel, value) => {
                            if (channel == channelName && !value.IsNullOrEmpty)
                            {
                                subject.OnNext(value);
                            }
                        }));

                    return subs;
                }
            });

            return subject;
        }

        /// <summary>
        /// Subscribe to a channel
        /// </summary>
        public ValueTask<IObservable<object>> SubscribeToChannel(string channelName, Type type)
        {
            var s = State;
            return GetSubject(channelName)
                .Map(subj =>
                    subj.Select(value => Deserialise(channelName, type, s))
                        .Where(x => x.IsSome)
                        .Select(x => x.ValueUnsafe()));
        }

        /// <summary>
        /// Subscribe to a channel
        /// </summary>
        public ValueTask<IObservable<A>> SubscribeToChannel<A>(string channelName)
        {
            var s = State;
            return GetSubject(channelName)
                .Map(subj =>
                    subj.Select(value => Deserialise<A>(channelName, s))
                        .Where(x => x.IsSome)
                        .Select(x => x.ValueUnsafe()));
        }

        /// <summary>
        /// Unsubscribe from a channel
        /// </summary>
        public async ValueTask<Unit> UnsubscribeChannel(string channelName)
        {
            channelName += State.Config.CatalogueName;

            await RetryAsync(State, async s => await s.Conn.GetSubscriber().UnsubscribeAsync(channelName));
            
            State.Subs.Swap(subs => {

                if (subs.ContainsKey(channelName))
                {
                    subs[channelName].OnCompleted();
                    subs = subs.Remove(channelName);
                }

                return subs;
            });
            return unit;
        }
        
        /// <summary>
        /// Set a value
        /// </summary>
        public ValueTask<Unit> SetValue<A>(string key, A value) =>
            RetryAsyncIgnore(State, async s => await s.Db.StringSetAsync(
                key, 
                Serialise(value, s),
                TimeSpan.FromDays(RedisCluster.maxDaysToPersistProcessState)));

        /// <summary>
        /// Get a value
        /// </summary>
        public ValueTask<A> GetValue<A>(string key) =>
            RetryAsync(State, async s => Deserialise<A>(await s.Db.StringGetAsync(key), s))
                .Map(ov => ov.IsSome
                    ? (A) ov
                    : throw new SerializationException($"{key} cannot be deserialised to type {typeof(A).FullName}"));

        /// <summary>
        /// Key exists?
        /// </summary>
        public ValueTask<bool> Exists(string key) =>
            RetryAsync(State, async s => await s.Db.KeyExistsAsync(key));

        /// <summary>
        /// Enqueue an item on the end of a list
        /// </summary>
        public ValueTask<int> Enqueue<A>(string key, A value) =>
            RetryAsync(State, async s => (int) await s.Db.ListRightPushAsync(key, Serialise(value, s)));

        /// <summary>
        /// Dequeue the first item in a list
        /// </summary>
        public ValueTask<Option<A>> Dequeue<A>(string key) =>
            RetryAsync(State, async s => Deserialise<A>(await s.Db.ListLeftPopAsync(key), s));

        /// <summary>
        /// Get all values in a queue
        /// </summary>
        /// <param name="key"></param>
        /// <typeparam name="A"></typeparam>
        /// <returns></returns>
        public async ValueTask<Seq<A>> GetQueue<A>(string key) =>
            await Exists(key)
                ? await RetryAsync(State, async s =>
                    (await s.Db.ListRangeAsync(key))
                               .Choose(x => Deserialise<A>(x, s))
                               .ToSeq())
                : Empty;

        /// <summary>
        /// Delete a key
        /// </summary>
        public ValueTask<bool> Delete(string key) =>
            RetryAsync(State, async s => await s.Db.KeyDeleteAsync(key));

        /// <summary>
        /// Delete multiple keys
        /// </summary>
        public ValueTask<bool> DeleteMany(params string[] keys) =>
            DeleteMany(keys.ToSeq());

        /// <summary>
        /// Delete multiple keys
        /// </summary>
        public ValueTask<bool> DeleteMany(Seq<string> keys) =>
            RetryAsync(State, async s => await s.Db.KeyDeleteAsync(keys.Map(k => (RedisKey) k).ToArray()) == keys.Count());

        /// <summary>
        /// Peek at the head item in a queue
        /// </summary>
        public ValueTask<Option<A>> Peek<A>(string key) =>
            RetryAsync(State, async s => await s.Db.ListGetByIndexAsync(key, 0))
                .Map(x => Deserialise<A>(x, s));

        /// <summary>
        /// The length of a queue
        /// </summary>
        public ValueTask<int> QueueLength(string key) =>
            RetryAsync(State, async s => (int) await s.Db.ListLengthAsync(key));

        /// <summary>
        /// Runs a query on all servers in the Redis cluster for the key specified
        /// with a prefix and suffix applied.  Returns a list of Redis keys
        /// </summary>
        /// <remarks>
        /// Wildcard is *
        /// </remarks>
        Seq<string> QueryKeys(string keyQuery, string prefix, string suffix)
        {
            return Go(State, $"{prefix}{keyQuery}{suffix}");
            
            Seq<string> Go(IntState st, string ibxkey)
            {
                Seq<string> xs = Empty; 
                
                foreach (var ep in st.Conn.GetEndPoints())
                {
                    var sv = st.Conn.GetServer(ep);
                    foreach (var redisKey in sv.Keys(st.DbNum, ibxkey))
                    {
                        xs = xs.Add((string) redisKey);
                    }
                }
                return xs;
            }
        }


        /// <summary>
        /// Find the registered process names for a specified role
        /// </summary>
        public ValueTask<Seq<ProcessName>> QueryRegistered(string role, string keyQuery)
        {
            var prefix = $"{regdPrefix}{role}-";
            
            return (from strKey in QueryKeys(keyQuery, prefix, "")
                    select new ProcessName(strKey.Substring(prefix.Length)))
                   .AsValueTask();
        }

        /// <summary>
        /// Find the processes that match the key
        /// </summary>
        public ValueTask<Seq<ProcessId>> QueryProcesses(string keyQuery) =>
            QueryKeys(keyQuery, "", userInboxSuffix)
                .Map(strKey => new ProcessId(strKey.Substring(0, strKey.Length - userInboxSuffix.Length)))
                .AsValueTask();

        /// <summary>
        /// Query metadata for keys that match the query
        /// </summary>
        public async ValueTask<HashMap<ProcessId, ProcessMetaData>> QueryProcessMetaData(string keyQuery)
        {
            var nkeys = QueryKeys(keyQuery, "", metaDataSuffix).Map(x => (RedisKey) x).ToArray();

            return toHashMap(await MapKeys(nkeys, State));

            async ValueTask<System.Collections.Generic.IEnumerable<(ProcessId, ProcessMetaData)>> MapKeys(RedisKey[] keys, IntState st) =>
                keys.Map(x => (string)x)
                    .Map(x => (ProcessId)x.Substring(0 ,x.Length - metaDataSuffix.Length))
                    .Zip(await RetryAsync(st, async s => await s.Db.StringGetAsync(keys))
                                  .Map(xs => xs.Choose(x => Deserialise<ProcessMetaData>(x, st)).ToArray()));
        }
        
        /// <summary>
        /// Query session keys
        /// </summary>
        /// <returns></returns>
        public ValueTask<Seq<string>> QuerySessionKeys() =>
            QueryKeys("sys-session-*", "", "").AsValueTask();

        /// <summary>
        /// Query scheduled keys
        /// </summary>
        public ValueTask<Seq<string>> QueryScheduleKeys(string system) =>
            QueryKeys($"/__schedule/{system}/*", "", "").AsValueTask();

        /// <summary>
        /// Return true if a field in a hash-map exists
        /// </summary>
        public ValueTask<bool> HashFieldExists(string key, string field) => 
            RetryAsync(State, async s => await s.Db.HashExistsAsync(key, field));

        /// <summary>
        /// Adds or update the hash-map field to with the value supplied. Creates a new key and field if key or
        /// field does not exist
        /// </summary>
        public ValueTask<Unit> HashFieldAddOrUpdate<A>(string key, string field, A value) =>
            RetryAsyncIgnore(State, async s =>
                await s.Db.HashSetAsync(key, field, Serialise(value, s)));

        /// <summary>
        /// Adds or update the hash-map fields to values provided. Creates a new key and fields if key or fields
        /// do not exist
        /// </summary>
        public ValueTask<Unit> HashFieldAddOrUpdate<A>(string key, HashMap<string, A> fields) =>
            RetryAsync(State, async s =>
                await s.Db.HashSetAsync(
                    key,
                    fields.AsEnumerable()
                        .Map(pair => new HashEntry(pair.Key, Serialise(pair.Value, s)))
                        .ToArray()
                ));

        /// <summary>
        /// Adds or updates the hash-map field to the value provided. Field is not set if key does not exist.
        /// </summary>
        public ValueTask<bool> HashFieldAddOrUpdateIfKeyExists<A>(string key, string field, A value) =>
            RetryAsync(State, async s => {
                var trans = s.Db.CreateTransaction();
                trans.AddCondition(Condition.KeyExists(key));
                await trans.HashSetAsync(key, field, Serialise(value, s));
                return await trans.ExecuteAsync();
            });

        /// <summary>
        /// Adds or update the hash-map fields to the values provided. Fields are not added if key does not exist.
        /// </summary>
        public ValueTask<bool> HashFieldAddOrUpdateIfKeyExists<A>(string key, HashMap<string, A> fields) =>
            RetryAsync(State, async s => {
                var trans = s.Db.CreateTransaction();
                trans.AddCondition(Condition.KeyExists(key));
                await trans.HashSetAsync(
                    key,
                    fields.AsEnumerable().Map(pair => new HashEntry(pair.Key, Serialise(pair.Value, s))).ToArray()
                );

                return await trans.ExecuteAsync();
            });

        /// <summary>
        /// Remove field from hash-map
        /// </summary>
        public ValueTask<bool> DeleteHashField(string key, string field) =>
            RetryAsync(State, async s => await s.Db.HashDeleteAsync(key, field));

        /// <summary>
        /// Remove hash-map fields
        /// </summary>
        public ValueTask<int> DeleteHashFields(string key, Seq<string> fields) =>
            RetryAsync(State, async s => (int) await s.Db.HashDeleteAsync(key, fields.Map(x => (RedisValue)x).ToArray()));

        /// <summary>
        /// Get all hash-map fields (typed) 
        /// </summary>
        public ValueTask<HashMap<string, A>> GetHashFields<A>(string key) =>
            RetryAsync(State, async s =>
                (await s.Db.HashGetAllAsync(key))
                    .Fold(
                        HashMap<string, A>(),
                        (m, e) => Deserialise<A>(e.Value, s)
                                      .Match(f => m.Add(e.Name, f), () => m))
                    .Filter(notnull));

        /// <summary>
        /// Get a field from a hash-map, deserialise it to an `A`
        /// </summary>
        public async ValueTask<Option<A>> GetHashFieldDropOnDeserialiseFailed<A>(string key, string field) =>
            await RetryAsync(State, async s => Deserialise<A>(await s.Db.HashGetAsync(key, field), s));
        
        /// <summary>
        /// Get hash-map fields
        /// </summary>
        public ValueTask<HashMap<K, V>> GetHashFields<K, V>(string key, Func<string, K> keyBuilder) =>
            RetryAsync(State, async s =>
                (await s.Db.HashGetAllAsync(key))
                    .Fold(
                        HashMap<K, V>(),
                        (m, e) => Deserialise<V>(e.Value, s)
                                     .Match(v => m.Add(keyBuilder(e.Name), v), () => m))
                    .Filter(notnull));

        /// <summary>
        /// Get a field in a hash-map
        /// </summary>
        public ValueTask<Option<A>> GetHashField<A>(string key, string field) =>
            RetryAsync(State, async s => Deserialise<A>(await s.Db.HashGetAsync(key, field), s));

        /// <summary>
        /// Get named hash fields
        /// </summary>
        public ValueTask<HashMap<string, A>> GetHashFields<A>(string key, Seq<string> fields) =>
            RetryAsync(State, async s =>
                (await s.Db.HashGetAsync(key, fields.Map(x => (RedisValue)x).ToArray()))
                    .Zip(fields)
                    .Fold(
                        HashMap<string, A>(),
                        (m, e) => Deserialise<A>(e.Item1, s)
                                    .Match(v => m.Add(e.Item2, v), () => m))
                    .Filter(notnull));

        /// <summary>
        /// Set: add or update  
        /// </summary>
        public ValueTask<Unit> SetAddOrUpdate<A>(string key, A value) =>
            RetryAsyncIgnore(State, async s => await s.Db.SetAddAsync(key, Serialise(value, s)));

        /// <summary>
        /// Remove a value from a set
        /// </summary>
        public ValueTask<Unit> SetRemove<A>(string key, A value) =>
            RetryAsyncIgnore(State, async s => await s.Db.SetRemoveAsync(key, Serialise(value, s)));

        /// <summary>
        /// Get a set
        /// </summary>
        public ValueTask<LanguageExt.HashSet<A>> GetSet<A>(string key) =>
            RetryAsync(State, async s => toHashSet((await s.Db.SetMembersAsync(key)).Choose(x => Deserialise<A>(x, s))));

        /// <summary>
        /// Does a value exist in a set?
        /// </summary>
        public ValueTask<bool> SetContains<A>(string key, A value) =>
            RetryAsync(State, async s => await s.Db.SetContainsAsync(key, Serialise(value, s)));

        /// <summary>
        /// Set a key to expire
        /// </summary>
        public ValueTask<bool> SetExpire(string key, TimeSpan time) =>
            RetryAsync(State, async s => await s.Db.KeyExpireAsync(key, time));
        
        static async ValueTask<A> RetryAsync<A>(IntState state, Func<IntState, ValueTask<A>> f)
        {
            try
            {
                return await f(state).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException || ex is RedisConnectionException || ex is RedisServerException)
            {
                return await retry(state, f).ConfigureAwait(false);
            }

            async ValueTask<A> retry(IntState istate, Func<IntState, ValueTask<A>> fn)
            {
                for (int i = 0; ; i++)
                {
                    try
                    {
                        return await fn(istate).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is TimeoutException || ex is RedisConnectionException || ex is RedisServerException)
                    {
                        if (i == TimeoutRetries)
                        {
                            throw;
                        }

                        // Backing off wait time
                        // 0 - immediately
                        // 1 - 100 ms
                        // 2 - 400 ms
                        // 3 - 900 ms
                        // 4 - 1600 ms
                        // Maximum wait == 3000ms
                        await Task.Delay(i * i * 100).ConfigureAwait(false);
                    }
                }
            }
        }

        static async ValueTask<Unit> RetryAsyncIgnore<A>(IntState state, Func<IntState, ValueTask<A>> f)
        {
            await RetryAsync(state, f).ConfigureAwait(false);
            return default;
        }

        static async ValueTask<Unit> RetryAsync(IntState state, Func<IntState, ValueTask> f)
        {
            try
            {
                await f(state).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException || ex is RedisConnectionException || ex is RedisServerException)
            {
                await retry(state, f).ConfigureAwait(false);
            }

            return unit;

            async ValueTask retry(IntState istate, Func<IntState, ValueTask> fn)
            {
                for (int i = 0; ; i++)
                {
                    try
                    {
                        await fn(istate);
                    }
                    catch (Exception ex) when (ex is TimeoutException || ex is RedisConnectionException || ex is RedisServerException)
                    {
                        if (i == TimeoutRetries)
                        {
                            throw;
                        }

                        // Backing off wait time
                        // 0 - immediately
                        // 1 - 100 ms
                        // 2 - 400 ms
                        // 3 - 900 ms
                        // 4 - 1600 ms
                        // Maximum wait == 3000ms
                        await Task.Delay(i * i * 100).ConfigureAwait(false);
                    }
                }
            }
        }
        
        static string Serialise<A>(A value, IntState st) =>
            st.SerialiseIO.Serialise(value);

        static Option<A> Deserialise<A>(RedisValue value, IntState st) =>
            value.IsNullOrEmpty
                ? None
                : st.SerialiseIO.DeserialiseStructural<A>(value);

        static Option<object> Deserialise(RedisValue value, Type type, IntState st) =>
            value.IsNullOrEmpty
                ? None
                : st.SerialiseIO.DeserialiseStructural(value, type);
        
        /// <summary>
        /// Internal state
        /// The state is represented as a struct with two members so that it can be passed through the
        /// Retry functions easily.  This gets around the limitation of referencing struct members from
        /// within lambdas.
        /// </summary>
        struct IntState
        {
            public IntState(ClusterConfig config, SerialiseIO serialiseIO) =>
                (Config, SerialiseIO) = (config, serialiseIO);
            
            /// <summary>
            /// Cluster configuration
            /// </summary>
            public readonly ClusterConfig Config;

            /// <summary>
            /// Serialise IO
            /// </summary>
            public readonly SerialiseIO SerialiseIO;
            
            /// <summary>
            /// Return true if connected to cluster
            /// </summary>
            public bool Connected =>
                Config.Connection.Value.IsSome;
            
            /// <summary>
            /// Redis DB access
            /// </summary>
            public IDatabase Db => 
                Config.Connection.Value.IsNone
                    ? throw new InvalidOperationException("Disconnected")
                    : ((RedisConn)Config.Connection.Value).redis.GetDatabase(((RedisConn)Config.Connection.Value).databaseNumber);

            /// <summary>
            /// Redis connection
            /// </summary>
            public ConnectionMultiplexer Conn
            {
                get
                {
                    var oconn = Config.Connection.Value;
                    return oconn.IsNone
                        ? throw new InvalidOperationException("Disconnected")
                        : ((RedisConn) oconn).redis;
                }
            }            

            public int DbNum
            {
                get
                {
                    var oconn = Config.Connection.Value;
                    return oconn.IsNone
                        ? throw new InvalidOperationException("Disconnected")
                        : ((RedisConn) oconn).databaseNumber;
                }
            }

            public Atom<HashMap<string, Subject<RedisValue>>> Subs
            {
                get
                {
                    var oconn = Config.Connection.Value;
                    return oconn.IsNone
                        ? throw new InvalidOperationException("Disconnected")
                        : ((RedisConn) oconn).subscriptions;
                }
            }
        }
    }
}