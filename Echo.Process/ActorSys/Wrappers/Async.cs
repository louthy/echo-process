#nullable enable
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;

namespace Echo;

internal static class Async
{
    public static Func<S> Setup<S>(Func<Task<S>> setup) =>
        () =>
        {
            S? state = default;
            Exception? err = null;
            using (var wait = new AutoResetEvent(false))
            {
                setup().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        err = t.Exception;
                    }
                    else
                    {
                        state = t.Result;
                    }
                    wait.Set();
                });
                wait.WaitOne();
            }

            if (err != null)
            {
                ExceptionDispatchInfo.Capture(err).Throw();
            }

            return state ?? throw new InvalidOperationException();
        };

    public static Func<S, A, S> Inbox<S, A>(Func<S, A, Task<S>> inbox) =>
        (s, m) =>
        {
            Exception? err = null;
            using (var wait = new AutoResetEvent(false))
            {
                inbox(s, m).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        err = t.Exception;
                    }
                    else
                    {
                        s = t.Result;
                    }
                    wait.Set();
                });
                wait.WaitOne();
            }

            if (err != null)
            {
                ExceptionDispatchInfo.Capture(err).Throw();
            }

            return s;
        };
    
    public static Func<S, Unit> Shutdown<S>(Func<S, Task<Unit>> shutdown) =>
        state =>
        {
            Exception? err = null;
            using (var wait = new AutoResetEvent(false))
            {
                shutdown(state).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        err = t.Exception;
                    }
                    wait.Set();
                });
                wait.WaitOne();
            }

            if (err != null)
            {
                ExceptionDispatchInfo.Capture(err).Throw();
            }
            return default(Unit);
        };
}    

