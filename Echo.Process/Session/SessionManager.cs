﻿using Newtonsoft.Json;
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
                c.GetAllHashFieldsInBatch(c.QuerySessionKeys().ToSeq())
                    .Map(sessions =>
                            sessions.Filter(vals => (from ts in vals.Find(LastAccessKey).Map(v => new DateTime((long)v))
                                                     from to in vals.Find(TimeoutKey).Map(v => (long)v)
                                                     where ts < now.AddSeconds(-to * 2)
                                                     select true).IfNone(false))
                                    .Keys)
                    .Map(Seq)
                    .Do(strandedSessions  => SupplementarySessionManager.removeSessionIdFromSuppMap(c, strandedSessions.Map(ReverseSessionKey)))
                    .Do(strandedSessions  => c.DeleteMany(strandedSessions))
                    .Map(strandedSessions => strandedSessions.Iter(sessionId => c.PublishToChannel(SessionsNotify, SessionAction.Stop(sessionId, system, nodeName)))); ;
                    


                // Remove session keys when an in-memory session ends
                ended = SessionEnded.Subscribe(sid => Stop(sid));

                touch = Sync.Touched.Subscribe(tup =>
                {
                    try
                    {
                        //check if the session has not been stopped in the meantime or expired
                        if(c.HashFieldAddOrUpdateIfKeyExists(SessionKey(tup.Item1), LastAccessKey, DateTime.UtcNow.Ticks))
                        {
                            c.PublishToChannel(SessionsNotify, SessionAction.Touch(tup.Item1, system, nodeName));
                        }
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

        static SessionId ReverseSessionKey(string sessionKey) => sessionKey.Substring(12);
        
        public LanguageExt.Unit Start(SessionId sessionId, int timeoutSeconds)
        {
            Sync.Start(sessionId, timeoutSeconds);
            cluster.Iter(c => c.HashFieldAddOrUpdate(SessionKey(sessionId), TimeoutKey, timeoutSeconds));
            return cluster.Iter(c => c.PublishToChannel(SessionsNotify, SessionAction.Start(sessionId, timeoutSeconds, system, nodeName)));
        }

        public LanguageExt.Unit Stop(SessionId sessionId)
        {
            Sync.Stop(sessionId);
            cluster.Iter(c =>
            {
                c.Delete(SessionKey(sessionId));
                SupplementarySessionManager.removeSessionIdFromSuppMap(c, sessionId);
            });

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
            var added = false;
            Sync.SetData(sessionId, key, value, time);

            cluster.Iter(c =>
            {
                added = c.HashFieldAddOrUpdateIfKeyExists(SessionKey(sessionId), key, SessionDataItemDTO.Create(value));

                if (added && key == SupplementarySessionId.Key && value is SupplementarySessionId supp)
                {
                    SupplementarySessionManager.setSuppSessionInSuppMap(c, sessionId, supp);
                }
            });

            return added
                   ? cluster.Iter(c => 
                        c.PublishToChannel(SessionsNotify, 
                            SessionAction.SetData(
                                time,
                                sessionId,
                                key,
                                value,
                                system,
                                nodeName)))
                   : unit;
        }

        public LanguageExt.Unit ClearData(long time, SessionId sessionId, string key)
        {
            Sync.ClearData(sessionId, key, time);

            cluster.Iter(c =>
            {
                c.DeleteHashField(SessionKey(sessionId), key);

                if (key == SupplementarySessionId.Key)
                {
                    SupplementarySessionManager.removeSessionIdFromSuppMap(c, sessionId);
                }
            });

            return cluster.Iter(c =>
                c.PublishToChannel(
                    SessionsNotify,
                    SessionAction.ClearData(time, sessionId, key, system, nodeName)));
        }
        
        Option<SessionId> getSessionIdFromCluster(SupplementarySessionId suppSessionId) =>
            from c  in cluster
            from s  in SupplementarySessionManager.getSessionIdFromSuppMap(c, suppSessionId)
            from to in c.GetHashField<int>(SessionKey(s), TimeoutKey)
            let  _  =  Sync.UpdateSupplementarySessionId(s, suppSessionId)
            select Sync.Start(s, to);
        
        /// <summary>
        /// Attempts to get a SessionId by supplementary session ID.  
        /// </summary>
        /// <param name="supplementarySessionId"></param>
        /// <returns></returns>
        public Option<SessionId> GetSessionId(SupplementarySessionId supplementarySessionId) => 
            Sync.GetSessionId(supplementarySessionId) || getSessionIdFromCluster(supplementarySessionId);

        /// <summary>
        /// Attempts to get a supplementary sessionId by session ID.  Only checked locally.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public Option<SupplementarySessionId> GetSupplementarySessionId(SessionId sessionId) =>
            Sync.GetSupplementarySessionId(sessionId);

    }

    static class SupplementarySessionManager
    {
        const string sessionToSuppKey = "sys-map-session-supp";
        const string suppToSessionKey = "sys-map-supp-session";

        internal static LanguageExt.Unit setSuppSessionInSuppMap(ICluster cluster, SessionId sessionId, SupplementarySessionId suppSessionId)
        {
            //delete any old supp-sessions to keep in sync
            removeSessionIdFromSuppMap(cluster, sessionId);

            cluster.HashFieldAddOrUpdate(sessionToSuppKey, sessionId.Value,     suppSessionId.Value);
            cluster.HashFieldAddOrUpdate(suppToSessionKey, suppSessionId.Value, sessionId.Value);
            return unit;
        }

        internal static LanguageExt.Unit removeSessionIdFromSuppMap(ICluster cluster, SessionId sessionId)
        {
            var supp = cluster.GetHashField<string>(sessionToSuppKey, sessionId.Value);
            cluster.DeleteHashField(sessionToSuppKey, sessionId.Value);
            supp.IfSome(s => cluster.DeleteHashField(suppToSessionKey, s));
            return unit;
        }

        /// <summary>
        /// remove multiple session ids at once
        /// </summary>
        /// <param name="cluster"></param>
        /// <param name="sessionIds"></param>
        /// <returns></returns>
        internal static LanguageExt.Unit removeSessionIdFromSuppMap(ICluster cluster, Seq<SessionId> sessionIds)
        {
            var sessionIdValues = sessionIds.Map(s => s.Value);

            var supps = cluster.GetHashFields<string>(sessionToSuppKey, sessionIdValues).Values.ToSeq();

            cluster.DeleteHashFields(sessionToSuppKey, sessionIdValues);
            cluster.DeleteHashFields(suppToSessionKey, supps);

            return unit;
        }

        internal static Option<SessionId> getSessionIdFromSuppMap(ICluster cluster, SupplementarySessionId sessionId) =>
            cluster.GetHashField<string>(suppToSessionKey, sessionId.Value).Map(v => new SessionId(v));
    }
}