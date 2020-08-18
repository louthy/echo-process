using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Echo
{
    public static partial class ProcessAff
    {
#if DEBUG
        /// <summary>
        /// Log info - Internal 
        /// </summary>
        internal static void logInfo(object message) =>
            Debug.WriteLine(new ProcessLogItem(ProcessLogItemType.Info, (message ?? "").ToString()));
#else
        /// <summary>
        /// Log info - Internal 
        /// </summary>
        internal static void logInfo(object message)
        {
        }
#endif 

        private static void IfNotNull<T>(T value, Action<T> action)
            where T : class
        {
            if (value != null) action(value);
        }

        /// <summary>
        /// Log warning - Internal 
        /// </summary>
        public static void logWarn(string message) =>
            IfNotNull(message, _ => log.OnNext(new ProcessLogItem(ProcessLogItemType.Warning, (message ?? "").ToString())));

        /// <summary>
        /// Log system error - Internal 
        /// </summary>
        internal static void logSysErr(string message) =>
            IfNotNull(message, _ => log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, (message ?? "").ToString())));

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        internal static void logSysErr(Exception ex) =>
            IfNotNull(ex, _ => log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, ex)));

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        internal static void logSysErr(string message, Exception ex) =>
            IfNotNull(message, _ => IfNotNull(ex, __ => log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, (message ?? "").ToString(), ex))));

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        public static void logUserErr(string message) =>
            IfNotNull(message, _ => log.OnNext(new ProcessLogItem(ProcessLogItemType.UserError, (message ?? "").ToString())));

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static void logErr(Exception ex) =>
            IfNotNull(ex, _ => log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, ex)));

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static void logErr(string message, Exception ex) =>
            IfNotNull(message, _ => IfNotNull(ex, __ => log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, (message ?? ""), ex))));

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static void logErr(string message) =>
            IfNotNull(message, _ => log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, (message ?? ""))));

        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Aff.  Always returns in a Succ state, using the
        /// defaultValue if necessary 
        /// </summary>
        public static Aff<RT, A> catchAndLogErr<RT, A>(Aff<RT, A> ma, A defaultValue, string message = null) where RT : struct, HasEcho<RT> =>
            AffMaybe<RT, A>(async env => {

                var res = await ma.RunIO(env);
                if (res.IsFail)
                {
                    if (message == null)
                    {
                        log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, (Error) res));
                    }
                    else
                    {
                        log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, message, (Error) res));
                    }

                    return Prelude.FinSucc(defaultValue);
                }
                else
                {
                    return res;
                }
            });

        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Eff in a Fail state, otherwise Succ 
        /// </summary>
        public static Aff<RT, A> logErr<RT, A>(Aff<RT, A> ma, string message = null) where RT : struct, HasEcho<RT> =>
            AffMaybe<RT, A>(async env => {

                var res = await ma.RunIO(env);
                if (res.IsFail)
                {
                    if (message == null)
                    {
                        log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, (Error) res));
                    }
                    else
                    {
                        log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, message, (Error) res));
                    }
                }
                return res;
            });

        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Eff.  Always returns in a Succ state, using the
        /// defaultValue if necessary 
        /// </summary>
        public static Eff<RT, A> catchAndLogErr<RT, A>(Eff<RT, A> ma, A defaultValue, string message = null) where RT : struct, HasEcho<RT> =>
            EffMaybe<RT, A>(env => {

                var res = ma.RunIO(env);
                if (res.IsFail)
                {
                    if (message == null)
                    {
                        log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, (Error) res));
                    }
                    else
                    {
                        log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, message, (Error) res));
                    }

                    return Prelude.FinSucc(defaultValue);
                }
                else
                {
                    return res;
                }
            });
        
        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Eff in a Fail state, otherwise Succ 
        /// </summary>
        public static Eff<RT, A> logErr<RT, A>(Eff<RT, A> ma, string message = null) where RT : struct, HasEcho<RT> =>
            EffMaybe<RT, A>(env => {

                var res = ma.RunIO(env);
                if (res.IsFail)
                {
                    if (message == null)
                    {
                        log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, (Error) res));
                    }
                    else
                    {
                        log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, message, (Error) res));
                    }
                }
                return res;
            });

        /// <summary>
        /// Log subject - Internal
        /// </summary>
        private static readonly Subject<ProcessLogItem> log = new Subject<ProcessLogItem>();
    }
}
