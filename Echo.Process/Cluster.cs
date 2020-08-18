 using System;
 using LanguageExt;
 using static LanguageExt.Prelude;

 namespace Echo
 {
     public static class Cluster
     {
         // TODO: Consider whether to move this static map into the RT, or whether that's too much overhead for the users
         
         static readonly Atom<HashMap<SystemName, ClusterConfig>> configMap = Atom(HashMap<SystemName, ClusterConfig>());
         
         /// <summary>
         /// Add a system config
         /// </summary>
         internal static Eff<RT, Unit> addSystem<RT>(SystemName system, ClusterConfig cfg) =>
             Eff(() => ignore(configMap.Swap(c => c.AddOrUpdate(system, cfg))));

         /// <summary>
         /// Remove a system config 
         /// </summary>
         internal static Eff<RT, Unit> removeSystem<RT>(SystemName system) =>
             Eff(() => ignore(configMap.Swap(c => c.Remove(system))));

         /// <summary>
         /// Get a system config or return None if it doesn't exist
         /// </summary>
         public static Option<ClusterConfig> config(SystemName system) =>
             configMap.Value.Find(system);

         /// <summary>
         /// Get a system config or throw an exception if it doesn't exist
         /// </summary>
         public static ClusterConfig configOrThrow(SystemName system) =>
             configMap.Value.Find(system).IfNone(() => throw new InvalidOperationException($"Echo system '{system}' not active"));

         /// <summary>
         /// Connect to the cluster
         /// </summary>
         public static Aff<RT, bool> connect<RT>() where RT : struct, HasEcho<RT> =>
             default(RT).EchoAff.MapAsync(async echoIO => await echoIO.ClusterConnect());

         /// <summary>
         /// Connect to the cluster
         /// </summary>
         public static Aff<RT, bool> disconnect<RT>() where RT : struct, HasEcho<RT> =>
             default(RT).EchoAff.MapAsync(async echoIO => await echoIO.ClusterDisconnect());
        
        /// <summary>
        /// Publish data to a named channel
        /// </summary>
        public static Aff<RT, int> publishToChannel<RT, A>(string channelName, A data) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.PublishToChannel(channelName, data));

        /// <summary>
        /// Subscribe to a named channel
        /// </summary>
        public static Aff<RT, IObservable<Object>> subscribeToChannel<RT>(string channelName, System.Type type) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.SubscribeToChannel(channelName, type));

        /// <summary>
        /// Subscribe to a named channel
        /// </summary>
        public static Aff<RT, IObservable<A>> subscribeToChannel<RT, A>(string channelName) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.SubscribeToChannel<A>(channelName));

        /// <summary>
        /// Unsubscribe from a channel (removes all subscribers from a channel)
        /// </summary>
        public static Aff<RT, Unit> unsubscribeChannel<RT>(string channelName) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.UnsubscribeChannel(channelName));

        /// <summary>
        /// Set a value by key
        /// </summary>
        public static Aff<RT, Unit> setValue<RT, A>(string key, A value) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.SetValue(key, value));

        /// <summary>
        /// Get a value by key
        /// </summary>
        public static Aff<RT, A> getValue<RT, A>(string key) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.GetValue<A>(key));

        /// <summary>
        /// Check if a key exists
        /// </summary>
        public static Aff<RT, bool> exists<RT>(string key) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.Exists(key));

        /// <summary>
        /// Enqueue a message
        /// </summary>
        public static Aff<RT, int> enqueue<RT, A>(string key, A value) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.Enqueue(key, value));

        /// <summary>
        /// Dequeue a message
        /// </summary>
        public static Aff<RT, Option<A>> dequeue<RT, A>(string key) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.Dequeue<A>(key));

        /// <summary>
        /// Get queue by key
        /// </summary>
        public static Aff<RT, Seq<A>> getQueue<RT, A>(string key) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.GetQueue<A>(key));

        /// <summary>
        /// Remove a key
        /// </summary>
        /// <param name="key">Key</param>
        public static Aff<RT, bool> delete<RT>(string key) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.Delete(key));

        /// <summary>
        /// Remove many keys
        /// </summary>
        /// <param name="keys">Keys</param>
        public static Aff<RT, bool> deleteMany<RT>(params string[] keys) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.DeleteMany(keys));

        /// <summary>
        /// Remove many keys
        /// </summary>
        /// <param name="keys">Keys</param>
        public static Aff<RT, bool> deleteMany<RT>(Seq<string> keys) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.DeleteMany(keys));

        /// <summary>
        /// Look at the item at the head of the queue
        /// </summary>
        public static Aff<RT, Option<A>> peek<RT, A>(string key) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.Peek<A>(key));

        /// <summary>
        /// Find the queue length
        /// </summary>
        /// <param name="key">Key</param>
        public static Aff<RT, int> queueLength<RT>(string key) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.QueueLength(key));

        /// <summary>
        /// Finds all registered names in a role
        /// </summary>
        /// <param name="role">Role to limit search to</param>
        /// <param name="keyQuery">Key query.  * is a wildcard</param>
        /// <returns>Registered names</returns>
        public static Aff<RT, Seq<ProcessName>> queryRegistered<RT>(string role, string keyQuery) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.QueryRegistered(role, keyQuery));

        /// <summary>
        /// Finds all the processes based on the search pattern provided.  Note the returned
        /// ProcessIds may contain processes that aren't currently active.  You can still post
        /// to them however.
        /// </summary>
        /// <param name="keyQuery">Key query.  * is a wildcard</param>
        /// <returns>Matching ProcessIds</returns>
        public static Aff<RT, Seq<ProcessId>> queryProcesses<RT>(string keyQuery) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.QueryProcesses(keyQuery));

        /// <summary>
        /// Finds all the processes based on the search pattern provided and then returns the
        /// meta-data associated with them.
        /// </summary>
        /// <param name="keyQuery">Key query.  * is a wildcard</param>
        /// <returns>Map of ProcessId to ProcessMetaData</returns>
        public static Aff<RT, HashMap<ProcessId, ProcessMetaData>> queryProcessMetaData<RT>(string keyQuery) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.QueryProcessMetaData(keyQuery));
        
        /// <summary>
        /// Finds all session keys
        /// </summary>
        /// <returns>Session keys</returns>
        public static Aff<RT, Seq<string>> querySessionKeys<RT>() where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.QuerySessionKeys());

        /// <summary>
        /// Query the keys of persisted scheduled items
        /// </summary>
        /// <param name="system"></param>
        /// <returns></returns>
        public static Aff<RT, Seq<string>> queryScheduleKeys<RT>(string system) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.QueryScheduleKeys(system));
        
        /// <summary>
        /// Return true if a field in a hash-map exists
        /// </summary>
        public static Aff<RT, bool> hashFieldExists<RT>(string key, string field) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.HashFieldExists(key, field));

        /// <summary>
        /// Adds or update the hash-map field to with the value supplied. Creates a new key and field if key or
        /// field does not exist
        /// </summary>
        public static Aff<RT, Unit> hashFieldAddOrUpdate<RT, A>(string key, string field, A value) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.HashFieldAddOrUpdate(key, field, value));

        /// <summary>
        /// Adds or update the hash-map fields to values provided. Creates a new key and fields if key or fields
        /// do not exist
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="fields"></param>
        public static Aff<RT, Unit> hashFieldAddOrUpdate<RT, A>(string key, HashMap<string, A> fields) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.HashFieldAddOrUpdate(key, fields));

        /// <summary>
        /// Adds or updates the hash-map field to the value provided. Field is not set if key does not exist.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <returns>true if added successfully</returns>
        public static Aff<RT, bool> hashFieldAddOrUpdateIfKeyExists<RT, A>(string key, string field, A value) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.HashFieldAddOrUpdateIfKeyExists(key, field, value));

        /// <summary>
        /// Adds or update the hash-map fields to the values provided. Fields are not added if key does not exist.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="fields"></param>
        /// <returns>true if added successfully</returns>
        public static Aff<RT, bool> hashFieldAddOrUpdateIfKeyExists<RT, A>(string key, HashMap<string, A> fields) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.HashFieldAddOrUpdateIfKeyExists(key, fields));

        /// <summary>
        /// Remove field from hash-map
        /// </summary>
        public static Aff<RT, bool> deleteHashField<RT>(string key, string field) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.DeleteHashField(key, field));

        /// <summary>
        /// Remove hash-map fields
        /// </summary>
        public static Aff<RT, int> deleteHashFields<RT>(string key, Seq<string> fields) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.DeleteHashFields(key, fields));
        
        /// <summary>
        /// Get all hash-map fields (typed) 
        /// </summary>
        public static Aff<RT, HashMap<string, A>> getHashFields<RT, A>(string key) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.GetHashFields<A>(key));
        
        /// <summary>
        /// Get a field from a hash-map, deserialise it to an `A`
        /// </summary>
        /// <returns>Some on success, None on not present or failure to deserialise</returns>
        public static Aff<RT, Option<A>> getHashFieldDropOnDeserialiseFailed<RT, A>(string key, string field) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.GetHashFieldDropOnDeserialiseFailed<A>(key, field));

        /// <summary>
        /// Get hash-map fields
        /// </summary>
        public static Aff<RT, HashMap<K, V>> getHashFields<RT, K, V>(string key, Func<string, K> keyBuilder) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.GetHashFields<K, V>(key, keyBuilder));
        
        /// <summary>
        /// Get a field in a hash-map
        /// </summary>
        public static Aff<RT, Option<A>> getHashField<RT, A>(string key, string field) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.GetHashField<A>(key, field));
        
        /// <summary>
        /// Get a set of fields from a hash-map
        /// </summary>
        public static Aff<RT, HashMap<string, A>> getHashFields<RT, A>(string key, Seq<string> fields) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.GetHashFields<A>(key, fields));
        
        /// <summary>
        /// Set: add or update  
        /// </summary>
        public static Aff<RT, Unit> setAddOrUpdate<RT, A>(string key, A value) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.SetAddOrUpdate(key, value));
        
        /// <summary>
        /// Remove a value from a set
        /// </summary>
        public static Aff<RT, Unit> setRemove<RT, A>(string key, A value) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.SetRemove(key, value));
        
        /// <summary>
        /// Get a set
        /// </summary>
        public static Aff<RT, HashSet<A>> getSet<RT, A>(string key) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.GetSet<A>(key));
        
        /// <summary>
        /// Does a value exist in a set?
        /// </summary>
        public static Aff<RT, bool> setContains<RT, A>(string key, A value) where RT : struct, HasEcho<RT> =>
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.SetContains(key, value));
        
        /// <summary>
        /// Set a key to expire
        /// </summary>
        public static Aff<RT, bool> setExpire<RT>(string key, TimeSpan time) where RT : struct, HasEcho<RT> =>         
            default(RT).EchoAff.MapAsync(async echoIO => await echoIO.SetExpire(key, time));
     }
 }

