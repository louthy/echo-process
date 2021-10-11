using LanguageExt;
using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Echo.Traits;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;

namespace Echo
{
    public static partial class Process<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        /// <summary>
        /// Publish a message for any listening subscribers
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        /// <param name="message">Message to publish</param>
        public static Aff<RT, Unit> publish<T>(T message) =>
            Eff(() => Process.publish(message));

        /// <summary>
        /// Publish a message for any listening subscribers, delayed.
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        /// <param name="message">Message to publish</param>
        /// <param name="delayFor">How long to delay sending for</param>
        /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
        /// for any other reason.</returns>
        public static Aff<RT, IDisposable> publish<T>(T message, TimeSpan delayFor) =>
            Eff(() => Process.publish(message, delayFor));

        /// <summary>
        /// Publish a message for any listening subscribers, delayed.
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// This will fail to be accurate across a Daylight Saving Time boundary
        /// </remarks>
        /// <param name="message">Message to publish</param>
        /// <param name="delayUntil">When to send</param>
        /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
        /// for any other reason.</returns>
        public static Aff<RT, IDisposable> publish<T>(T message, DateTime delayUntil) =>
            Eff(() => Process.publish(message, delayUntil));

        /// <summary>
        /// Subscribes our inbox to another process publish stream.  When it calls 'publish' it will
        /// arrive in our inbox.
        /// </summary>
        /// <param name="pid">Process to subscribe to</param>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        /// <returns>IDisposable, call IDispose to end the subscription</returns>
        public static Aff<RT, Unit> subscribe(ProcessId pid) =>
            Eff(() => Process.subscribe(pid));

        /// <summary>
        /// Unsubscribe from a process's publications
        /// </summary>
        /// <param name="pid">Process to unsub from</param>
        public static Aff<RT, Unit> unsubscribe(ProcessId pid) =>
            Eff(() => Process.unsubscribe(pid));

        /// <summary>
        /// Subscribe to the process publish stream.  When a process calls 'publish' it emits
        /// messages that can be consumed using this method.
        /// </summary>
        /// <remarks>
        /// The process can publish any number of types, any published messages not of type T will be ignored.
        /// 
        /// Because this call is asynchronous it could allow access to the message loop, therefore
        /// you can't call it from within a process message loop.
        /// </remarks>
        /// <returns>IDisposable, call IDispose to end the subscription</returns>
        public static Aff<RT, IDisposable> subscribe<T>(ProcessId pid, IObserver<T> observer) =>
            Eff(() => Process.subscribe(pid, observer));

        /// <summary>
        /// Subscribe to the process publish stream.  When a process calls 'publish' it emits
        /// messages that can be consumed using this method.
        /// </summary>
        /// <remarks>
        /// The process can publish any number of types, any published messages not of type T will be ignored.
        /// 
        /// Because this call is asynchronous it could allow access to the message loop, therefore
        /// you can't call it from within a process message loop.
        /// </remarks>
        /// <returns>IDisposable, call IDispose to end the subscription</returns>
        public static Aff<RT, IDisposable> subscribe<T>(ProcessId pid, Action<T> onNext, Action<Exception> onError, Action onComplete) =>
            Eff(() => Process.subscribe(pid, onNext, onError, onComplete));

        /// <summary>
        /// Subscribe to the process publish stream.  When a process calls 'publish' it emits
        /// messages that can be consumed using this method.
        /// </summary>
        /// <remarks>
        /// The process can publish any number of types, any published messages not of type T will be ignored.
        /// 
        /// Because this call is asynchronous it could allow access to the message loop, therefore
        /// you can't call it from within a process message loop.
        /// </remarks>
        public static Aff<RT, IDisposable> subscribe<T>(ProcessId pid, Action<T> onNext, Action<Exception> onError) =>
            Eff(() => Process.subscribe(pid, onNext, onError));

        /// <summary>
        /// Subscribe to the process publish stream.  When a process calls 'publish' it emits
        /// messages that can be consumed using this method.
        /// </summary>
        /// <remarks>
        /// The process can publish any number of types, any published messages not of type T will be ignored.
        /// 
        /// Because this call is asynchronous it could allow access to the message loop, therefore
        /// you can't call it from within a process message loop.
        /// </remarks>
        public static Aff<RT, IDisposable> subscribe<T>(ProcessId pid, Action<T> onNext) =>
            Eff(() => Process.subscribe(pid, onNext));

        /// <summary>
        /// Subscribe to the process publish stream.  When a process calls 'publish' it emits
        /// messages that can be consumed using this method.
        /// </summary>
        /// <remarks>
        /// The process can publish any number of types, any published messages not of type T will be ignored.
        /// 
        /// Because this call is asynchronous it could allow access to the message loop, therefore
        /// you can't call it from within a process message loop.
        /// </remarks>
        /// <returns>IDisposable, call IDispose to end the subscription</returns>
        public static Aff<RT, IDisposable> subscribe<T>(ProcessId pid, Action<T> onNext, Action onComplete) =>
            Eff(() => Process.subscribe(pid, onNext, onComplete));

        /// <summary>
        /// Get an IObservable for a process publish stream.  When a process calls 'publish' it emits
        /// messages on the observable returned by this method.
        /// </summary>
        /// <remarks>
        /// The process can publish any number of types, any published messages not of type T will be ignored.
        /// 
        /// Because this call is asynchronous it could allow access to the message loop, therefore
        /// you can't call it from within a process message loop.
        /// </remarks>
        /// <returns>IObservable T</returns>
        public static Aff<RT, IObservable<T>> observe<T>(ProcessId pid) =>
            Eff(() => Process.observe<T>(pid));

        /// <summary>
        /// Get an IObservable for a process publish stream.  When a process calls 'publish' it emits
        /// messages on the observable returned by this method.
        /// </summary>
        /// <remarks>
        /// The process can publish any number of types, any published messages not of type T will be ignored.
        /// 
        /// Because this call is asynchronous it could allow access to the message loop, that's why this 
        /// function is labelled `Unsafe`.  Careful disposing and capture of free variables is required to not
        /// break the principles of actors.
        /// </remarks>
        /// <returns>IObservable T</returns>
        public static Aff<RT, IObservable<T>> observeUnsafe<T>(ProcessId pid) =>
            Eff(() => Process.observeUnsafe<T>(pid));

        /// <summary>
        /// Get an IObservable for a process's state stream.  When a process state updates at the end of its
        /// message loop it announces it on the stream returned from this method.  You should use this for 
        /// notification only.  Never modify the state object belonging to a process.  Best practice is to make
        /// the state type immutable.
        /// </summary>
        /// <remarks>
        /// The process can publish any number of types, any published messages not of type T will be ignored.
        /// 
        /// Because this call is asynchronous it could allow access to the message loop, therefore
        /// you can't call it from within a process message loop.
        /// </remarks>
        /// <returns>IObservable T</returns>
        public static Aff<RT, IObservable<T>> observeState<T>(ProcessId pid) =>
            Eff(() => Process.observeState<T>(pid));

        /// <summary>
        /// Get an IObservable for a process's state stream.  When a process state updates at the end of its
        /// message loop it announces it on the stream returned from this method.  You should use this for 
        /// notification only.  Never modify the state object belonging to a process.  Best practice is to make
        /// the state type immutable.
        /// </summary>
        /// <remarks>
        /// The process can publish any number of types, any published messages not of type T will be ignored.
        /// 
        /// Because this call is asynchronous it could allow access to the message loop, that's why this 
        /// function is labelled `Unsafe`.  Careful disposing and capture of free variables is required to not
        /// break the principles of actors.
        /// </remarks>
        /// <returns>IObservable T</returns>
        public static Aff<RT, IObservable<T>> observeStateUnsafe<T>(ProcessId pid) =>
            Eff(() => Process.observeStateUnsafe<T>(pid));

        /// <summary>
        /// Subscribes our inbox to another process state publish stream.  
        /// When a process state updates at the end of its message loop it announces it arrives in our inbox.
        /// You should use this for notification only.  Never modify the state object belonging to a process.  
        /// Best practice is to make the state type immutable.
        /// </summary>
        /// <remarks>
        /// The process can publish any number of types, any published messages not of type T will be ignored.
        /// This should be used from within a process' message loop only
        /// </remarks>
        /// <returns></returns>
        public static Aff<RT, Unit> subscribeState<T>(ProcessId pid) =>
            Eff(() => Process.subscribeState<T>(pid));
    }
}
