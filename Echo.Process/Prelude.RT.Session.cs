using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using static LanguageExt.Prelude;
using LanguageExt.UnitsOfMeasure;
using System.Security.Cryptography;
using Echo.Session;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Echo.Traits;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Echo
{
    /// <summary>
    /// <para>
    ///     Process: Session functions
    /// </para>
    /// <para>
    ///     These functions facilitate the use of sessions that live from
    ///     Process to Process.  Essentially if there's an active Session
    ///     ID then it will be packaged with each message that is sent via
    ///     tell or ask.  
    /// </para>
    /// </summary>
    public static partial class Process<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        public static readonly IObservable<SessionId> SessionEnded = Process.SessionEnded;
        public static readonly IObservable<SessionId> SessionStarted = Process.SessionStarted;

        /// <summary>
        /// Starts a new session in the Process system with the specified
        /// session ID
        /// </summary>
        /// <param name="sid">Session ID</param>
        /// <param name="timeout">Session timeout</param>
        public static Aff<RT, SessionId> sessionStart(SessionId sid, Time timeout, SystemName system) =>
            Eff(() => Process.sessionStart(sid, timeout, system));

        /// <summary>
        /// Starts a new session in the Process system.  This variant must be called from
        /// within a Process, use the variant where you specify the SystemName to use it
        /// from outside
        /// </summary>
        /// <param name="sid">Session ID</param>
        /// <param name="timeout">Session timeout</param>
        /// <returns>sid</returns>
        public static Aff<RT, SessionId> sessionStart(SessionId sid, Time timeout) =>
            Eff(() => Process.sessionStart(sid, timeout));
        
        /// <summary>
        /// Starts a new session in the Process system.  This variant must be called from
        /// within a Process, use the variant where you specify the SystemName to use it
        /// from outside
        /// </summary>
        /// <param name="timeout">Session timeout</param>
        /// <returns>Session ID of the newly created session</returns>
        public static Aff<RT, SessionId> sessionStart(Time timeout) =>
            Eff(() => Process.sessionStart(timeout));

        /// <summary>
        /// Ends a session in the Process system with the specified
        /// session ID
        /// </summary>
        public static Aff<RT, Unit> sessionStop() =>
            Eff(Process.sessionStop);

        /// <summary>
        /// Touch a session
        /// Time-stamps the session so that its time-to-expiry is reset
        /// </summary>
        public static Aff<RT, Unit> sessionTouch() =>
            Eff(Process.sessionTouch);

        /// <summary>
        /// Touch a session
        /// Time-stamps the session so that its time-to-expiry is reset and also
        /// sets the current session ID. This should be used from inside of the 
        /// Process system to 'acquire' an existing session.  This is useful for
        /// web-requests for example to set the current session ID and to indicate
        /// activity.
        /// </summary>
        /// <param name="sid">Session ID</param>
        public static Aff<RT, Unit> sessionTouch(SessionId sid) =>
            Eff(() => Process.sessionTouch(sid));

        /// <summary>
        /// Sets the current session to the provided sessionid
        /// </summary>
        /// <param name="sid"></param>
        /// <returns></returns>
        public static Aff<RT, Unit> setSession(SessionId sid) =>
            Eff(() => Process.setSession(sid));

        /// <summary>
        /// Gets the current session ID
        /// </summary>
        /// <remarks>Also touches the session so that its time-to-expiry 
        /// is reset</remarks>
        /// <returns>Optional session ID</returns>
        public static Eff<RT, Option<SessionId>> sessionId() =>
            Eff(Process.sessionId);

        /// <summary>
        /// Set the meta-data to store with the current session, this is typically
        /// user credentials when they've logged in.  But can be anything.  It is a 
        /// key/value store that is sync'd around the cluster.
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Data value </param>
        public static Aff<RT, Unit> sessionSetData(string key, object value) =>
            Eff(() => Process.sessionSetData(key, value));


        /// <summary>
        /// Clear the meta-data key stored with the session
        /// </summary>
        /// <param name="key">Key</param>
        public static Aff<RT, Unit> sessionClearData(string key) =>
            Eff(() => Process.sessionClearData(key));
        
        /// <summary>
        /// Get the meta-data stored with the session.  
        /// <para>
        /// The session system allows concurrent updates from across the
        /// cluster or from within the app-domain (from multiple processes).
        /// To maintain the integrity of the data in any one session, the system 
        /// uses a version clock per-key.
        /// </para>
        /// <para>
        /// That means that if two Processes update the session from the
        /// same 'time' start point, then there will be a conflict and the session 
        /// will  contain both values stored against the key.  
        /// </para>
        /// <para>
        /// It is up to you  to decide on the best approach to resolving the conflict.  
        /// Calling Head() / HeadOrNone() on the result will get the value that was 
        /// written first, calling Last() will get the value that was written last.
        /// However, being first or last doesn't necessarily make a value 'right', in
        /// an asynchronous system the last value could be the newest or oldest.
        /// Both value commits had the same starting state, so if the consistency
        /// of the session data is important to you then you should implement
        /// a more robust strategy to deal with value conflicts, if integrity doesn't
        /// really matter, call HeadOrNone().
        /// </para>
        /// <para>
        /// The versioning system is closest to Lamport Clocks.  Eventually this 
        /// implementation will be replaced with a Dotted Version Vector system.
        /// </para>
        /// </summary>
        /// <param name="key">Key</param>
        public static Aff<RT, Seq<T>> sessionGetData<T>(string key) =>
            Eff(() => Process.sessionGetData<T>(key));

        /// <summary>
        /// Returns True if there is a session ID available.  NOTE: That
        /// doesn't mean the session is still alive.
        /// </summary>
        /// <returns></returns>
        public static Eff<RT, bool> hasSession() =>
            Eff(Process.hasSession);


        /// <summary>
        /// Acquires a session for the duration of invocation of the 
        /// provided function. NOTE: This does not create a session, or
        /// check that a session exists.  
        /// </summary>
        /// <param name="sid">Session ID</param>
        /// <param name="f">Function to invoke</param>
        /// <returns>Result of the function</returns>
        public static Eff<RT, R> withSession<R>(SessionId sid, Func<R> f) =>
            Eff(() => Process.withSession(sid, f));

        /// <summary>
        /// Acquires a session ID for the duration of invocation of the 
        /// provided action.  NOTE: This does not create a session, or
        /// check that a session exists.  
        /// </summary>
        /// <param name="sid">Session ID</param>
        /// <param name="f">Action to invoke</param>
        public static Eff<RT, Unit> withSession(SessionId sid, Action f) =>
            Eff(() => Process.withSession(sid, f));
        
        /// <summary>
        /// Acquires a session for the duration of invocation of the 
        /// provided function. NOTE: This does not create a session, or
        /// check that a session exists.  
        /// </summary>
        /// <param name="sid">Session ID</param>
        /// <param name="f">Function to invoke</param>
        /// <returns>Result of the function</returns>
        public static Aff<RT, R> withSession<R>(SessionId sid, Func<Eff<RT, R>> f) =>
            EffMaybe<RT, R>(rt => Process.withSession(sid, () => f().Run(rt)));
        
        /// <summary>
        /// Acquires a session for the duration of invocation of the 
        /// provided function. NOTE: This does not create a session, or
        /// check that a session exists.  
        /// </summary>
        /// <param name="sid">Session ID</param>
        /// <param name="f">Function to invoke</param>
        /// <returns>Result of the function</returns>
        public static Aff<RT, R> withSession<R>(SessionId sid, Func<Aff<RT, R>> f) =>
            AffMaybe<RT, R>(async rt => await Process.withSession(sid, () => f().Run(rt)).ConfigureAwait(false));

        /// <summary>
        /// Returns true if the current session is active, returns false if not in message loop
        /// or session is not active.
        /// </summary>
        /// <returns></returns>
        public static Eff<RT, bool> hasActiveSession() =>
            Eff(Process.hasActiveSession);

        /// <summary>
        /// Adds a supplementary session id to existing session. If the current session already has a supplmentary session 
        /// replace it
        /// </summary>
        /// <param name="sid"></param>
        /// <returns></returns>
        public static Aff<RT, Unit> addSupplementarySession(SupplementarySessionId sid) =>
            Eff(() => Process.addSupplementarySession(sid));

        /// <summary>
        /// Gets a echo session id which contains a supplementary session 
        /// </summary>
        /// <param name="sid"></param>
        /// <returns></returns>
        public static Aff<RT, Option<SessionId>> getSessionBySupplementaryId(SupplementarySessionId sid) =>
            Eff(() => Process.getSessionBySupplementaryId(sid));

        /// <summary>
        /// Get supplementary session id for current session
        /// </summary>
        /// <returns></returns>
        public static Aff<RT, Option<SupplementarySessionId>> getSupplementarySessionId() =>
            Eff(Process.getSupplementarySessionId);

        /// <summary>
        /// Get supplementary session id for current session. If not exists, create a new one.
        /// </summary>
        /// <returns></returns>
        public static Aff<RT, SupplementarySessionId> provideSupplementarySessionId() =>
            Eff(Process.provideSupplementarySessionId);
    }
}
