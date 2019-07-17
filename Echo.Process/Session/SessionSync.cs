using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading;
using System.Reactive.Threading.Tasks;
using static LanguageExt.Prelude;
using static Echo.Process;
using System.Reactive.Concurrency;
using LanguageExt;

namespace Echo.Session
{
    /// <summary>
    /// Manages the in-memory view of the sessions
    /// </summary>
    class SessionSync
    {
        SystemName system;
        ProcessName nodeName;
        HashMap<SessionId, SessionVector> sessions;
        HashMap<SessionId, Subject<(SessionId, DateTime)>> sessionTouched;
        SupplementarySessions suppSessions = new SupplementarySessions();
        Subject<(SessionId, DateTime)> touched = new Subject<(SessionId, DateTime)>();

        VectorConflictStrategy strategy;
        object sync = new object();

        public SessionSync(SystemName system, ProcessName nodeName, VectorConflictStrategy strategy)
        {
            this.system = system;
            this.nodeName = nodeName;
            this.strategy = strategy;
        }

        public IObservable<(SessionId, DateTime)> Touched => 
            touched;

        public void ExpiredCheck()
        {
            var now = DateTime.UtcNow;
            sessions.Filter(s => s.Expires < now)
                    .Iter((sid,_) =>
                    {
                        try
                        {
                            sessionEnded.OnNext(sid);
                        }
                        catch(Exception e)
                        {
                            logErr(e);
                        }
                    });
        }

        public int Incoming(SessionAction incoming)
        {
            if (incoming.SystemName == system.Value && incoming.NodeName == nodeName.Value) return 0;

            switch(incoming.Tag)
            { 
                case SessionActionTag.Touch:
                    MarkTouched(incoming.SessionId);
                    break;
                case SessionActionTag.Start:
                    Start(incoming.SessionId, incoming.Timeout);
                    break;
                case SessionActionTag.Stop:
                    Stop(incoming.SessionId);
                    break;
                case SessionActionTag.SetData:
                    // See if the session in question is interested in the data that's incoming. 
                    var __ = from s in sessions.Find(incoming.SessionId)
                             from _ in s.Data.Find(incoming.Key)
                             from o in SessionDataTypeResolve.TryDeserialise(incoming.Value, incoming.Type)
                                                             .MapLeft(SessionDataTypeResolve.DeserialiseFailed(incoming.Value, incoming.Type))
                                                             .ToOption()
                             select SetData(incoming.SessionId, incoming.Key, o, incoming.Time);
                    break;
                case SessionActionTag.ClearData:
                    ClearData(incoming.SessionId, incoming.Key, incoming.Time);
                    break;
                default:
                    return 0;
            }
            return 1;
        }

        /// <summary>
        /// Get an active session
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public Option<SessionVector> GetSession(SessionId sessionId) =>
            sessions.Find(sessionId);

        internal Option<SessionVector> GetSession(Option<SessionId> sessionId) =>
            from sid in sessionId
            from ses in sessions.Find(sid)
            select ses;

        /// <summary>
        /// Gets an active session id using the supplementary session id
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        internal Option<SessionId> GetSessionId(SupplementarySessionId sessionId) => 
            suppSessions.GetSessionId(sessionId);

        /// <summary>
        /// Gets a supplementary session id using an active session id
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        internal Option<SupplementarySessionId> GetSupplementarySessionId(SessionId sessionId) => 
            suppSessions.GetSupplementarySessionId(sessionId);

        /// <summary>
        /// Updates the suppSessionMap
        /// </summary>
        /// <param name="suppSessionId"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        internal LanguageExt.Unit UpdateSupplementarySessionId(SessionId sessionId, SupplementarySessionId suppSessionId) =>
            suppSessions.Update(sessionId, suppSessionId);

        /// <summary>
        /// Start a new session
        /// </summary>
        public SessionId Start(SessionId sessionId, int timeoutSeconds)
        {
            lock (sync)
            {
                if (sessions.ContainsKey(sessionId))
                {
                    return sessionId;
                }
                var session = SessionVector.Create(timeoutSeconds, VectorConflictStrategy.First);
                sessions = sessions.Add(sessionId, session);

                // Create a subject per session that will buffer touches so we don't push too
                // much over the net when not needed.  This routes to a single stream that isn't
                // buffered.
                var touch = new Subject<(SessionId, DateTime)>();
                touch.Sample(TimeSpan.FromSeconds(1))
                     .ObserveOn(TaskPoolScheduler.Default)
                     .Subscribe(touched.OnNext);
                sessionTouched = sessionTouched.Add(sessionId, touch);
            }
            return sessionId;
        }

        /// <summary>
        /// Remove a session and its state
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="vector"></param>
        public LanguageExt.Unit Stop(SessionId sessionId)
        {
            lock (sync)
            {
                // When it comes to stopping sessions we just get rid of the whole history
                // regardless of whether something happened in the future.  Session IDs
                // are randomly generated, so any future event with the same session ID
                // is a deleted future.
                sessions = sessions.Remove(sessionId);
                sessionTouched.Find(sessionId).Iter(sub => sub.OnCompleted());
                sessionTouched = sessionTouched.Remove(sessionId);
            }

            suppSessions.Remove(sessionId);

            return unit;
        }

        /// <summary>
        /// Timestamp a sessions to keep it alive and publishes the change
        /// </summary>
        public LanguageExt.Unit Touch(SessionId sessionId) =>
            sessions.Find(sessionId).IfSome(s =>
            {
                s.Touch();
                sessionTouched.Find(sessionId).Iter(sub => sub.OnNext((sessionId, DateTime.UtcNow)));
            });

        /// <summary>
        /// Timestamp a sessions to keep it alive locally
        /// </summary>
        LanguageExt.Unit MarkTouched(SessionId sessionId) =>
            sessions.Find(sessionId).IfSome(s =>
            {
                s.Touch();
            });

        /// <summary>
        /// Set data on the session key/value store
        /// </summary>
        public LanguageExt.Unit SetData(SessionId sessionId, string key, object value, long vector)
        {
            if(key == SupplementarySessionId.Key && value is SupplementarySessionId supp)
            {
                suppSessions.Update(sessionId, supp);
            }

            return sessions.Find(sessionId).Iter(s => s.SetKeyValue(vector, key, value, strategy));
        }

        /// <summary>
        /// Clear a session key
        /// </summary>
        public LanguageExt.Unit ClearData(SessionId sessionId, string key, long vector) =>
            sessions.Find(sessionId).Iter(s =>
            {
                if (key == SupplementarySessionId.Key)
                {
                    suppSessions.Remove(sessionId);
                }

                s.ClearKeyValue(vector, key);
            });

        /// <summary>
        /// Clear a session key
        /// </summary>
        public Option<ValueVector> GetExistingData(SessionId sessionId, string key) =>
            sessions.Find(sessionId).Bind(s => s.GetExistingData(key));

        /// <summary>
        /// a two way dictionary implementation for session and supplementary sesssions
        /// </summary>
        class SupplementarySessions
        {
            HashMap<SessionId, SupplementarySessionId> sessionToSupp;
            HashMap<SupplementarySessionId, SessionId> suppToSession;
            object sync = new object();

            /// <summary>
            /// Gets session id from supplementary sessionId
            /// </summary>
            /// <param name="sessionId"></param>
            /// <returns></returns>
            internal Option<SessionId> GetSessionId(SupplementarySessionId sessionId) => suppToSession.Find(sessionId);

            /// <summary>
            /// Gets a supplementary session id from sessionId
            /// </summary>
            /// <param name="sessionId"></param>
            /// <returns></returns>
            internal Option<SupplementarySessionId> GetSupplementarySessionId(SessionId sessionId) => sessionToSupp.Find(sessionId);

            /// <summary>
            /// update session id or supplementary sessionId
            /// </summary>
            /// <param name="sessionId"></param>
            /// <param name="suppSessionId"></param>
            /// <returns></returns>
            internal LanguageExt.Unit Update(SessionId sessionId, SupplementarySessionId suppSessionId)
            {
                //remove old supp-sessions:
                Remove(sessionId);
                lock (sync)
                {
                    suppToSession = suppToSession.AddOrUpdate(suppSessionId, sessionId);
                    sessionToSupp = sessionToSupp.AddOrUpdate(sessionId, suppSessionId);
                }

                return unit;
            }

            /// <summary>
            /// remove session id from the map
            /// </summary>
            /// <param name="sessionId"></param>
            /// <returns></returns>
            internal LanguageExt.Unit Remove(SessionId sessionId)
            {
                var suppSessionId = sessionToSupp.Find(sessionId);

                lock(sync)
                {
                    suppSessionId.IfSome(supp => suppToSession = suppToSession.Remove(supp));
                    sessionToSupp = sessionToSupp.Remove(sessionId);
                }

                return unit;
            }

        }
    }
}
