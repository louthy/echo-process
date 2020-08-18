using System;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Attributes;
using LanguageExt.Interfaces;
using static LanguageExt.Prelude;

namespace Echo
{
    public class EchoEnv
    {
        public readonly static EchoEnv Default = new EchoEnv(default, default, default, default);
        
        /// <summary>
        /// Current system
        /// </summary>
        public readonly SystemName SystemName;
        
        /// <summary>
        /// Current session ID
        /// </summary>
        internal readonly Option<SessionId> SessionId;
        
        /// <summary>
        /// True if we're currently in a message-loop
        /// </summary>
        internal readonly bool InMessageLoop;

        /// <summary>
        /// Current request
        /// </summary>
        internal readonly Option<ActorRequestContext> Request;

        /// <summary>
        /// Ctor
        /// </summary>
        internal EchoEnv(
            SystemName systemName,
            Option<SessionId> sessionId,
            bool inMessageLoop,
            Option<ActorRequestContext> request) =>
            (SystemName, SessionId, InMessageLoop, Request) = 
                (systemName, sessionId, inMessageLoop, request);

        /// <summary>
        /// Constructor function 
        /// </summary>
        public static EchoEnv New(SystemName systemName) =>
            new EchoEnv(systemName, None, false, None);
        
        /// <summary>
        /// Set the request
        /// </summary>
        internal EchoEnv WithRequest(ActorRequestContext request) =>
            new EchoEnv(SystemName, SessionId, true, request);

        /// <summary>
        /// Set the session
        /// </summary>
        internal EchoEnv WithSession(SessionId sid) =>
            new EchoEnv(SystemName, sid, InMessageLoop, Request);

        /// <summary>
        /// Set the session
        /// </summary>
        internal EchoEnv WithSystem(SystemName system) =>
            new EchoEnv(system, SessionId, InMessageLoop, Request);
    }

    /// <summary>
    /// Serialisation IO
    /// </summary>
    public interface SerialiseIO
    {
        string Serialise<A>(A value);
        Option<A> DeserialiseExact<A>(string value);
        Option<A> DeserialiseStructural<A>(string value);
        Option<object> DeserialiseExact(string value, Type type);
        Option<object> DeserialiseStructural(string value, Type type);
    }

    /// <summary>
    /// Type-class giving a struct the trait of supporting echo IO
    /// </summary>
    /// <typeparam name="RT">Runtime</typeparam>
    [Typeclass("*")]
    public interface HasSerialisation<RT>
        where RT : struct, HasCancel<RT>
    {

        /// <summary>
        /// Access to the echo IO
        /// </summary>
        Aff<RT, SerialiseIO> SerialiseAff { get; }

        /// <summary>
        /// Access to the echo IO
        /// </summary>
        Eff<RT, SerialiseIO> SerialiseEff { get; }
    }

    /// <summary>
    /// Type-class giving a struct the trait of supporting echo IO
    /// </summary>
    /// <typeparam name="RT">Runtime</typeparam>
    [Typeclass("*")]
    public interface HasEcho<RT> : HasCancel<RT>, HasEncoding<RT>, HasTime<RT>, HasFile<RT>, HasSerialisation<RT>
        where RT : 
            struct, 
            HasCancel<RT>, 
            HasEncoding<RT>,
            HasTime<RT>,
            HasFile<RT>,
            HasSerialisation<RT>
    {
        /// <summary>
        /// Access to the echo environment
        /// </summary>
        EchoEnv EchoEnv { get; }

        /// <summary>
        /// Access to the echo IO
        /// </summary>
        Aff<RT, EchoIO> EchoAff { get; }

        /// <summary>
        /// Access to the echo IO
        /// </summary>
        Eff<RT, EchoIO> EchoEff { get; }

        /// <summary>
        /// Use a local environment
        /// </summary>
        /// <remarks>This is used as the echo system steps into the scope of various processes to set the context
        /// for those processes</remarks>
        RT LocalEchoEnv(EchoEnv echoEnv);
    }

    public interface EchoIO
    {
        /// <summary>
        /// Connect to the persistence store
        /// </summary>
        /// <returns>True if connection was successful, or if already connected</returns>
        ValueTask<bool> ClusterConnect();

        /// <summary>
        /// Disconnect from the persistence store
        /// </summary>
        /// <returns>True if disconnection was successful, or if already disconnected</returns>
        ValueTask<bool> ClusterDisconnect();
        
        /// <summary>
        /// Publish data to a named channel
        /// </summary>
        ValueTask<int> PublishToChannel<A>(string channelName, A data);

        /// <summary>
        /// Subscribe to a named channel
        /// </summary>
        ValueTask<IObservable<Object>> SubscribeToChannel(string channelName, System.Type type);

        /// <summary>
        /// Subscribe to a named channel
        /// </summary>
        ValueTask<IObservable<A>> SubscribeToChannel<A>(string channelName);

        /// <summary>
        /// Unsubscribe from a channel (removes all subscribers from a channel)
        /// </summary>
        ValueTask<Unit> UnsubscribeChannel(string channelName);

        /// <summary>
        /// Set a value by key
        /// </summary>
        ValueTask<Unit> SetValue<A>(string key, A value);

        /// <summary>
        /// Get a value by key
        /// </summary>
        ValueTask<A> GetValue<A>(string key);

        /// <summary>
        /// Check if a key exists
        /// </summary>
        ValueTask<bool> Exists(string key);

        /// <summary>
        /// Enqueue a message
        /// </summary>
        ValueTask<int> Enqueue<A>(string key, A value);

        /// <summary>
        /// Dequeue a message
        /// </summary>
        ValueTask<Option<A>> Dequeue<A>(string key);

        /// <summary>
        /// Get queue by key
        /// </summary>
        ValueTask<Seq<A>> GetQueue<A>(string key);

        /// <summary>
        /// Remove a key
        /// </summary>
        /// <param name="key">Key</param>
        ValueTask<bool> Delete(string key);

        /// <summary>
        /// Remove many keys
        /// </summary>
        /// <param name="keys">Keys</param>
        ValueTask<bool> DeleteMany(params string[] keys);

        /// <summary>
        /// Remove many keys
        /// </summary>
        /// <param name="keys">Keys</param>
        ValueTask<bool> DeleteMany(Seq<string> keys);

        /// <summary>
        /// Look at the item at the head of the queue
        /// </summary>
        ValueTask<Option<A>> Peek<A>(string key);

        /// <summary>
        /// Find the queue length
        /// </summary>
        /// <param name="key">Key</param>
        ValueTask<int> QueueLength(string key);

        /// <summary>
        /// Finds all registered names in a role
        /// </summary>
        /// <param name="role">Role to limit search to</param>
        /// <param name="keyQuery">Key query.  * is a wildcard</param>
        /// <returns>Registered names</returns>
        ValueTask<Seq<ProcessName>> QueryRegistered(string role, string keyQuery);

        /// <summary>
        /// Finds all the processes based on the search pattern provided.  Note the returned
        /// ProcessIds may contain processes that aren't currently active.  You can still post
        /// to them however.
        /// </summary>
        /// <param name="keyQuery">Key query.  * is a wildcard</param>
        /// <returns>Matching ProcessIds</returns>
        ValueTask<Seq<ProcessId>> QueryProcesses(string keyQuery);

        /// <summary>
        /// Finds all the processes based on the search pattern provided and then returns the
        /// meta-data associated with them.
        /// </summary>
        /// <param name="keyQuery">Key query.  * is a wildcard</param>
        /// <returns>Map of ProcessId to ProcessMetaData</returns>
        ValueTask<HashMap<ProcessId, ProcessMetaData>> QueryProcessMetaData(string keyQuery);
        
        /// <summary>
        /// Finds all session keys
        /// </summary>
        /// <returns>Session keys</returns>
        ValueTask<Seq<string>> QuerySessionKeys();

        /// <summary>
        /// Query the keys of persisted scheduled items
        /// </summary>
        /// <param name="system"></param>
        /// <returns></returns>
        ValueTask<Seq<string>> QueryScheduleKeys(string system);
        
        
        /// <summary>
        /// Return true if a field in a hash-map exists
        /// </summary>
        ValueTask<bool> HashFieldExists(string key, string field);

        /// <summary>
        /// Adds or update the hash-map field to with the value supplied. Creates a new key and field if key or
        /// field does not exist
        /// </summary>
        ValueTask<Unit> HashFieldAddOrUpdate<A>(string key, string field, A value);

        /// <summary>
        /// Adds or update the hash-map fields to values provided. Creates a new key and fields if key or fields
        /// do not exist
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="fields"></param>
        ValueTask<Unit> HashFieldAddOrUpdate<A>(string key, HashMap<string, A> fields);

        /// <summary>
        /// Adds or updates the hash-map field to the value provided. Field is not set if key does not exist.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <returns>true if added successfully</returns>
        ValueTask<bool> HashFieldAddOrUpdateIfKeyExists<A>(string key, string field, A value);

        /// <summary>
        /// Adds or update the hash-map fields to the values provided. Fields are not added if key does not exist.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="fields"></param>
        /// <returns>true if added successfully</returns>
        ValueTask<bool> HashFieldAddOrUpdateIfKeyExists<A>(string key, HashMap<string, A> fields);

        /// <summary>
        /// Remove field from hash-map
        /// </summary>
        ValueTask<bool> DeleteHashField(string key, string field);

        /// <summary>
        /// Remove hash-map fields
        /// </summary>
        ValueTask<int> DeleteHashFields(string key, Seq<string> fields);
        
        /// <summary>
        /// Get all hash-map fields (typed) 
        /// </summary>
        ValueTask<HashMap<string, A>> GetHashFields<A>(string key);
        
        /// <summary>
        /// Get a field from a hash-map, deserialise it to an `A`
        /// </summary>
        /// <returns>Some on success, None on not present or failure to deserialise</returns>
        ValueTask<Option<A>> GetHashFieldDropOnDeserialiseFailed<A>(string key, string field);

        /// <summary>
        /// Get hash-map fields
        /// </summary>
        ValueTask<HashMap<K, V>> GetHashFields<K, V>(string key, Func<string, K> keyBuilder);
        
        /// <summary>
        /// Get a field in a hash-map
        /// </summary>
        ValueTask<Option<A>> GetHashField<A>(string key, string field);
        
        /// <summary>
        /// Get a set of fields from a hash-map
        /// </summary>
        ValueTask<HashMap<string, A>> GetHashFields<A>(string key, Seq<string> fields);
        
        /// <summary>
        /// Set: add or update  
        /// </summary>
        ValueTask<Unit> SetAddOrUpdate<A>(string key, A value);
        
        /// <summary>
        /// Remove a value from a set
        /// </summary>
        ValueTask<Unit> SetRemove<A>(string key, A value);
        
        /// <summary>
        /// Get a set
        /// </summary>
        ValueTask<HashSet<A>> GetSet<A>(string key);
        
        /// <summary>
        /// Does a value exist in a set?
        /// </summary>
        ValueTask<bool> SetContains<A>(string key, A value);
        
        /// <summary>
        /// Set a key to expire
        /// </summary>
        ValueTask<bool> SetExpire(string key, TimeSpan time);
    }
}