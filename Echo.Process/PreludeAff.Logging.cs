using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Echo
{
    public static partial class ProcessAff<RT>
        where RT : struct, HasEcho<RT>
    {
        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Aff.  Always returns in a Succ state, using the
        /// defaultValue if necessary 
        /// </summary>
        public static Aff<RT, A> catchAndLogErr<A>(Aff<RT, A> ma, Aff<RT, A> defaultValue, string message = null) =>
            AffMaybe<RT, A>(async env => {

                var res = await ma.RunIO(env).ConfigureAwait(false);
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

                    return await defaultValue.RunIO(env).ConfigureAwait(false);
                }
                else
                {
                    return res;
                }
            });

        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Eff in a Fail state, otherwise Succ 
        /// </summary>
        public static Aff<RT, A> logErr<A>(Aff<RT, A> ma, string message = null) =>
            AffMaybe<RT, A>(async env => {

                var res = await ma.RunIO(env).ConfigureAwait(false);
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
        public static Eff<RT, A> catchAndLogErr<A>(Eff<RT, A> ma, Eff<RT, A> defaultValue, string message = null) =>
            EffMaybe<RT, A>(env => {

                var res = ma.RunIO(env);
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

                    return defaultValue.RunIO(env);
                }
                else
                {
                    return res;
                }
            });
        
        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Eff in a Fail state, otherwise Succ 
        /// </summary>
        public static Eff<RT, A> logErr<A>(Eff<RT, A> ma, string message = null) =>
            EffMaybe<RT, A>(env => {

                var res = ma.RunIO(env);
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
        public static Aff<RT, A> catchAndLogSysErr<A>(Aff<RT, A> ma, Aff<RT, A> defaultValue, string message = null) =>
            AffMaybe<RT, A>(async env => {

                var res = await ma.RunIO(env).ConfigureAwait(false);
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

                    return await defaultValue.RunIO(env).ConfigureAwait(false);
                }
                else
                {
                    return res;
                }
            });
        
        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Eff in a Fail state, otherwise Succ 
        /// </summary>
        public static Aff<RT, A> logSysErr<A>(Aff<RT, A> ma, string message = null) =>
            AffMaybe<RT, A>(async env => {

                var res = await ma.RunIO(env).ConfigureAwait(false);
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
        public static Eff<RT, A> catchAndLogSysErr<A>(Eff<RT, A> ma, Eff<RT, A> defaultValue, string message = null) =>
            EffMaybe<RT, A>(env => {

                var res = ma.RunIO(env);
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

                    return defaultValue.RunIO(env);
                }
                else
                {
                    return res;
                }
            });
        
        /// <summary>
        /// Logs any exception thrown by `ma` and returns the Eff in a Fail state, otherwise Succ 
        /// </summary>
        public static Eff<RT, A> logSysErr<A>(Eff<RT, A> ma, string message = null) =>
            EffMaybe<RT, A>(env => {

                var res = ma.RunIO(env);
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
