using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Echo
{
    public static partial class ProcessEff
    {
#if DEBUG
        /// <summary>
        /// Log info - Internal 
        /// </summary>
        internal static EffPure<Unit> logInfo(object message) =>
            Eff(() => {
                Debug.WriteLine(new ProcessLogItem(ProcessLogItemType.Info, (message ?? "").ToString()));
                return unit;
            });
#else
        /// <summary>
        /// Log info - Internal 
        /// </summary>
        internal static EffPure<Unit> logInfo(object message) => 
            unitEff;
#endif 

        private static Unit IfNotNull<A>(A value, Action<A> action)
            where A : class =>
                isnull(value)
                    ? unit
                    : fun(action)(value);

        static EffPure<Unit> onNext(string value, ProcessLogItemType type) =>
            Eff(() =>
                IfNotNull(value, _ => Process.log.OnNext(new ProcessLogItem(type, value.ToString()))));

        static EffPure<Unit> onNext(Exception value, ProcessLogItemType type) =>
            Eff(() =>
                IfNotNull(value, _ => Process.log.OnNext(new ProcessLogItem(type, value))));

        static EffPure<Unit> onNext(string message, Exception value, ProcessLogItemType type) =>
            Eff(() =>
                IfNotNull(message, _ => IfNotNull(value, _ => Process.log.OnNext(new ProcessLogItem(type, message, value)))));

        /// <summary>
        /// Log warning - Internal 
        /// </summary>
        public static EffPure<Unit> logWarn(string message) =>
            onNext(message, ProcessLogItemType.Warning);

        /// <summary>
        /// Log system error - Internal 
        /// </summary>
        internal static EffPure<Unit> logSysErr(string message) =>
            onNext(message, ProcessLogItemType.SysError);

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        internal static EffPure<Unit> logSysErr(Exception ex) =>
            onNext(ex, ProcessLogItemType.SysError);

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        internal static EffPure<Unit> logSysErr(Error ex) =>
            onNext(ex, ProcessLogItemType.SysError);

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        internal static EffPure<Unit> logSysErr(string message, Exception ex) =>
            onNext(message, ex, ProcessLogItemType.SysError);

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        internal static EffPure<Unit> logSysErr(string message, Error ex) =>
            onNext(message, ex, ProcessLogItemType.SysError);

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        public static EffPure<Unit> logUserErr(string message) =>
            onNext(message, ProcessLogItemType.UserError);

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static EffPure<Unit> logErr(Exception ex) =>
            onNext(ex, ProcessLogItemType.Error);

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static EffPure<Unit> logErr(Error ex) =>
            onNext(ex, ProcessLogItemType.Error);

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static EffPure<Unit> logErr(string message, Exception ex) =>
            onNext(message, ex, ProcessLogItemType.Error);

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static EffPure<Unit> logErr(string message, Error ex) =>
            onNext(message, ex, ProcessLogItemType.Error);

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static EffPure<Unit> logErr(string message) =>
            onNext(message,  ProcessLogItemType.Error);

        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Aff.  Always returns in a Succ state, using the
        /// defaultValue if necessary 
        /// </summary>
        public static AffPure<A> catchAndLogErr<A>(AffPure<A> ma, AffPure<A> defaultValue, string message = null) =>
            AffMaybe<A>(async () => {

                var res = await ma.RunIO().ConfigureAwait(false);
                if (res.IsFail)
                {
                    if (message == null)
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, (Error) res));
                    }
                    else
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, message, (Error) res));
                    }

                    return await defaultValue.RunIO().ConfigureAwait(false);
                }
                else
                {
                    return res;
                }
            });
        
        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Eff in a Fail state, otherwise Succ 
        /// </summary>
        public static AffPure<A> logErr<A>(AffPure<A> ma, string message = null) =>
            AffMaybe<A>(async () => {

                var res = await ma.RunIO().ConfigureAwait(false);
                if (res.IsFail)
                {
                    if (message == null)
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, (Error) res));
                    }
                    else
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, message, (Error) res));
                    }
                }
                return res;
            });
        
        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Eff.  Always returns in a Succ state, using the
        /// defaultValue if necessary 
        /// </summary>
        public static EffPure<A> catchAndLogErr<A>(EffPure<A> ma, EffPure<A> defaultValue, string message = null) =>
            EffMaybe<A>(() => {

                var res = ma.RunIO();
                if (res.IsFail)
                {
                    if (message == null)
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, (Error) res));
                    }
                    else
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, message, (Error) res));
                    }

                    return defaultValue.RunIO();
                }
                else
                {
                    return res;
                }
            });
        
        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Eff in a Fail state, otherwise Succ 
        /// </summary>
        public static EffPure<A> logErr<A>(EffPure<A> ma, string message = null) =>
            EffMaybe<A>(() => {

                var res = ma.RunIO();
                if (res.IsFail)
                {
                    if (message == null)
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, (Error) res));
                    }
                    else
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, message, (Error) res));
                    }
                }
                return res;
            });
        
        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Aff.  Always returns in a Succ state, using the
        /// defaultValue if necessary 
        /// </summary>
        public static AffPure<A> catchAndLogSysErr<A>(AffPure<A> ma, AffPure<A> defaultValue, string message = null) =>
            AffMaybe<A>(async () => {

                var res = await ma.RunIO().ConfigureAwait(false);
                if (res.IsFail)
                {
                    if (message == null)
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, (Error) res));
                    }
                    else
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, message, (Error) res));
                    }

                    return await defaultValue.RunIO().ConfigureAwait(false);
                }
                else
                {
                    return res;
                }
            });

        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Eff in a Fail state, otherwise Succ 
        /// </summary>
        public static AffPure<A> logSysErr<A>(AffPure<A> ma, string message = null) =>
            AffMaybe<A>(async () => {

                var res = await ma.RunIO().ConfigureAwait(false);
                if (res.IsFail)
                {
                    if (message == null)
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, (Error) res));
                    }
                    else
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, message, (Error) res));
                    }
                }
                return res;
            });
 
        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Eff.  Always returns in a Succ state, using the
        /// defaultValue if necessary 
        /// </summary>
        public static EffPure<A> catchAndLogSysErr<A>(EffPure<A> ma, EffPure<A> defaultValue, string message = null) =>
            EffMaybe<A>(() => {

                var res = ma.RunIO();
                if (res.IsFail)
                {
                    if (message == null)
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, (Error) res));
                    }
                    else
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, message, (Error) res));
                    }

                    return defaultValue.RunIO();
                }
                else
                {
                    return res;
                }
            });
        
        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Eff in a Fail state, otherwise Succ 
        /// </summary>
        public static EffPure<A> logSysErr<A>(EffPure<A> ma, string message = null) =>
            EffMaybe<A>(() => {

                var res = ma.RunIO();
                if (res.IsFail)
                {
                    if (message == null)
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, (Error) res));
                    }
                    else
                    {
                        Process.log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, message, (Error) res));
                    }
                }
                return res;
            });
    }
}
