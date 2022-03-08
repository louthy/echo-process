using System;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using static LanguageExt.Prelude;
using LanguageExt;

namespace Echo
{
    public static partial class Process
    {
        /// <summary>
        /// Use in message loop exception
        /// </summary>
        internal static T raiseUseInMsgLoopOnlyException<T>(string what) =>
            failwith<T>($"'{what}' should be used from within a process' message loop only");

        /// <summary>
        /// Use in message loop & in session exception
        /// </summary>
        internal static T raiseUseInMsgAndInSessionLoopOnlyException<T>(string what) =>
            failwith<T>($"'{what}' should be used from within a process' message loop and within session only");

        /// <summary>
        /// Not in message loop exception
        /// </summary>
        internal static T raiseDontUseInMessageLoopException<T>(string what) =>
            failwith<T>($"'{what}' should not be be used from within a process' message loop.");

        /// <summary>
        /// Returns true if in a message loop
        /// </summary>
        internal static bool InMessageLoop =>
            ActorContext.InMessageLoop;

        static Subject<SystemName> shutdownSubj = new Subject<SystemName>();
        static Subject<ShutdownCancellationToken> preShutdownSubj = new Subject<ShutdownCancellationToken>();

        internal static void OnShutdown(SystemName system)
        {
            shutdownSubj.OnNext(system);
            shutdownSubj.OnCompleted();
        }

        internal static void OnPreShutdown(ShutdownCancellationToken cancel)
        {
            preShutdownSubj.OnNext(cancel);
            if (!cancel.Cancelled)
            {
                preShutdownSubj.OnCompleted();
            }
        }

        internal static IDisposable safedelay(Action f, TimeSpan delayFor)
        {
            var savedContext = ActorContext.Request;
            var savedSession = ActorContext.SessionId;
            var stackTrace   = new System.Diagnostics.StackTrace(true);

            return Observable.Timer(delayFor).Do(_ =>
            {
                if (savedContext == null)
                {
                    f();
                }
                else
                {
                    ActorSystem system;
                    try
                    {

                        system = ActorContext.System(savedContext.Self.Actor.Id);
                    }
                    catch (Exception e)
                    {
                        throw new ProcessSystemException(e, stackTrace);
                    }

                    Task.Run(() => system.WithContext(
                                         savedContext.Self,
                                         savedContext.Parent,
                                         savedContext.Sender,
                                         savedContext.CurrentRequest,
                                         savedContext.CurrentMsg,
                                         savedSession,
                                         () => {
                                             f();

                                             // Run the operations that affect the settings and sending of tells
                                             // in the order which they occured in the actor
                                             ActorContext.Request?.Ops?.Run();
                                             return unit.AsValueTask();
                                         }));
                }
            }).Subscribe(onNext: _ => { }, onCompleted: () => { }, onError: logErr);
        }

        internal static IDisposable safedelay(Action f, DateTime delayUntil) =>
             safedelay(f, delayUntil - DateTime.UtcNow);

        /// <summary>
        /// Not advised to use this directly, but allows access to the underlying data-store.
        /// </summary>
        public static Option<ICluster> SystemCluster(SystemName system = default(SystemName)) => 
            ActorContext.System(system).Cluster;

        public static ProcessId resolvePID(ProcessId pid) =>
            ActorContext.ResolvePID(pid);
    }
}
