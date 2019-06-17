using Newtonsoft.Json;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading;
using System.Reactive.Threading.Tasks;
using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;

namespace Echo.Session
{
    /// <summary>
    /// Session manager for a single actor-system
    /// Receives direct session actions locally and remote session action messages
    /// Routes them through to SessionSync to manage the local state
    /// </summary>
    class SessionManager : IDisposable
    {
        const string SessionsNotify = "sys-sessions-notify";
        public readonly SessionSync Sync;

        readonly Option<ICluster> cluster;
        readonly SystemName system;
        readonly ProcessName nodeName;
        IDisposable notify;
        IDisposable ended;
        IDisposable touch;

        public SessionManager(Option<ICluster> cluster, SystemName system, ProcessName nodeName, VectorConflictStrategy strategy)
        {
            this.cluster = cluster;
            this.system = system;
            this.nodeName = nodeName;

            Sync = new SessionSync(system, nodeName, strategy);

            cluster.Iter(c =>
            {
                notify = c.SubscribeToChannel<SessionAction>(SessionsNotify).Subscribe(act => Sync.Incoming(act));

                var now = DateTime.UtcNow;

                // Look for stranded sessions that haven't been removed properly.  This is done once only
                // on startup because the systems should be shutting down sessions on their own.  This just
                // catches the situation where an app-domain died without shutting down properly.
                var strandedSessions = c.QuerySessionKeys()
                                        .Map(key =>
                                                from ts in c.GetHashField<long>(key, LastAccessKey)
                                                from to in c.GetHashField<int>(key, TimeoutKey)
                                                where new DateTime(ts) < now.AddSeconds(-to * 2)   // Multiply by 2, just to catch genuine non-active sessions
                                                select key).Somes().ToSeq();


                strandedSessions.Iter(s => c.Delete(s));

                c.GetHashFields(SupplementarySessionId.Key)
                    .Where(sid => strandedSessions.Exists(k => k == SessionKey(sid as string)))
                    .Iter(field => c.DeleteHashField(SupplementarySessionId.Key, field.Key));


                // Remove session keys when an in-memory session ends
                ended = SessionEnded.Subscribe(sid => Stop(sid));

                touch = Sync.Touched.Subscribe(tup =>
                {
                    try
                    {
                        c.HashFieldAddOrUpdate(SessionKey(tup.Item1), LastAccessKey, DateTime.UtcNow.Ticks);
                        c.PublishToChannel(SessionsNotify, SessionAction.Touch(tup.Item1, system, nodeName));
                    }
                    catch(Exception e)
                    {
                        logErr(e);
                    }
                });
            });
        }

        public void Dispose()
        {
            notify?.Dispose();
            ended?.Dispose();
            touch?.Dispose();
        }

        /// <summary>
        /// Attempts to get a SessionVector by ID.  If it doesn't exist then there's a chance that
        /// we're out-of-sync.  So go to the cluster and look for a session by that ID.  If one
        /// exists then start a local session with that ID and the details stored in the cluster.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public Option<SessionVector> GetSession(SessionId sessionId) =>
            Sync.GetSession(sessionId) || Sync.GetSession(from c in cluster
                                                          from to in c.GetHashField<int>(SessionKey(sessionId), TimeoutKey)
                                                          select Sync.Start(sessionId, to));

        const string LastAccessKey = "__last-access";
        const string TimeoutKey = "__timeout";
        
        static string SessionKey(SessionId sessionId) =>
            $"sys-session-{sessionId}";
        
        public LanguageExt.Unit Start(SessionId sessionId, int timeoutSeconds)
        {
            Sync.Start(sessionId, timeoutSeconds);
            cluster.Iter(c => c.HashFieldAddOrUpdate(SessionKey(sessionId), TimeoutKey, timeoutSeconds));
            return cluster.Iter(c => c.PublishToChannel(SessionsNotify, SessionAction.Start(sessionId, timeoutSeconds, system, nodeName)));
        }

        public LanguageExt.Unit Stop(SessionId sessionId)
        {
            Sync.Stop(sessionId);
            cluster.Iter(c => c.Delete(SessionKey(sessionId)));
            deleteSuppSessionInClusterMap(sessionId);
            return cluster.Iter(c => c.PublishToChannel(SessionsNotify, SessionAction.Stop(sessionId, system, nodeName)));
        }

        public LanguageExt.Unit Touch(SessionId sessionId) =>
            Sync.Touch(sessionId);

        /// <summary>
        /// gets session meta data associated with a session id using the provided key
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public Option<ValueVector> GetDataByKey(SessionId sessionId, string key) =>
            from c in cluster
            from f in c.GetHashFieldDropOnDeserialiseFailed<SessionDataItemDTO>(SessionKey(sessionId), key)
            from o in SessionDataTypeResolve.TryDeserialise(f.SerialisedData, f.Type)
                                            .MapLeft(SessionDataTypeResolve.DeserialiseFailed(f.SerialisedData, f.Type))
                                            .ToOption()
            select ValueVector.New(0, o);
        
        public LanguageExt.Unit SetData(long time, SessionId sessionId, string key, object value)
        {
            Sync.SetData(sessionId, key, value, time);
            cluster.Iter(c => c.HashFieldAddOrUpdate(SessionKey(sessionId), key, SessionDataItemDTO.Create(value)));

            if(key == SupplementarySessionId.Key && value is SupplementarySessionId supp)
            {
                setSuppSessionInClusterMap(supp, sessionId);
            }
             
            return cluster.Iter(c => c.PublishToChannel(SessionsNotify, SessionAction.SetData(
                time,
                sessionId,
                key,
                value,
                system,
                nodeName
            )));
        }

        public LanguageExt.Unit ClearData(long time, SessionId sessionId, string key)
        {
            if (key == SupplementarySessionId.Key)
            {
                deleteSuppSessionInClusterMap(sessionId);
            }

            Sync.ClearData(sessionId, key, time);
            cluster.Iter(c => c.DeleteHashField(SessionKey(sessionId), key));

            return cluster.Iter(c =>
                c.PublishToChannel(
                    SessionsNotify,
                    SessionAction.ClearData(time, sessionId, key, system, nodeName)));
        }

        LanguageExt.Unit setSuppSessionInClusterMap(SupplementarySessionId suppSessionId, SessionId sessionId) =>
            cluster.Iter(c => c.HashFieldAddOrUpdate(SupplementarySessionId.Key, suppSessionId.Value, sessionId.Value));

        LanguageExt.Unit deleteSuppSessionInClusterMap(SessionId sessionId) =>
            ignore(from c   in cluster.ToSeq()
                   from key in c.GetHashFields(SupplementarySessionId.Key)
                                .Where(o => (o as string) == sessionId.Value)
                                .Map(v => v.Key).ToSeq()
                   select c.DeleteHashField(SupplementarySessionId.Key, key));

        Option<SessionId> getSessionIdFromSuppSessionClusterMap(SupplementarySessionId suppSessionId) =>
            from c  in cluster
            from s  in c.GetHashField<string>(SupplementarySessionId.Key, suppSessionId.Value).Map(v => new SessionId(v))
            from to in c.GetHashField<int>(SessionKey(s), TimeoutKey)
            let  _  =  Sync.UpdateSupplementarySessionId(suppSessionId, s)
            select Sync.Start(s, to);


        /// <summary>
        /// Attempts to get a SessionId by supplementary session ID.  
        /// </summary>
        /// <param name="supplementarySessionId"></param>
        /// <returns></returns>
        public Option<SessionId> GetSessionId(SupplementarySessionId supplementarySessionId) => 
            Sync.GetSessionId(supplementarySessionId) || getSessionIdFromSuppSessionClusterMap(supplementarySessionId);

        /// <summary>
        /// Attempts to get a supplementary sessionId by session ID.  Only checked locally.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public Option<SupplementarySessionId> GetSupplementarySessionId(SessionId sessionId) =>
            Sync.GetSupplementarySessionId(sessionId);
    }
}