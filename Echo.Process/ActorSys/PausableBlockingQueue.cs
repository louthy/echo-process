using Echo;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys
{
    public class PausableBlockingQueue<A> : IDisposable
    {
        readonly EventWaitHandle pauseWait = new AutoResetEvent(true);
        readonly BlockingCollection<A> items;
        readonly CancellationTokenSource tokenSource;
        readonly CancellationToken token;

        volatile bool paused;
        public bool IsPaused => paused;
        public bool IsCancelled => token.IsCancellationRequested;

        public PausableBlockingQueue(int boundedCapacity)
        {
            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;
            items = new BlockingCollection<A>(boundedCapacity);
        }

        public int Count =>
            items.Count;

        public IDisposable ReceiveAsync<S>(S state, Func<S, A, InboxDirective> handler) =>
            (IDisposable)Task.Factory.StartNew(() =>
            {
                bool addToFrontOfQueue = false;
                var directive = default(InboxDirective);

                foreach (var item in items.GetConsumingEnumerable(token))
                {
                    do
                    {
                        if (token.IsCancellationRequested) return;

                        if (IsPaused)
                        {
                            pauseWait?.WaitOne();
                        }

                        try
                        {
                            directive = handler(state, item);
                        }
                        catch (Exception e)
                        {
                            Process.logErr(e);
                        }

                        switch (directive)
                        {
                            case InboxDirective.Pause:
                                addToFrontOfQueue = false;
                                Pause();
                                break;

                            case InboxDirective.Shutdown:
                                addToFrontOfQueue = false;
                                Cancel();
                                return;

                            case InboxDirective.PushToFrontOfQueue:
                                addToFrontOfQueue = true;
                                break;

                            case InboxDirective.Default:
                                addToFrontOfQueue = false;
                                break;
                        }
                    }
                    while (addToFrontOfQueue);
                }
            }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        public void Post(A value)
        {
            if (!items.TryAdd(value))
            {
                throw new QueueFullException();
            }
        }

        public void Cancel()
        {
            tokenSource?.Cancel();
            pauseWait?.Set();
        }

        public void Pause() =>
            paused = true;

        public void UnPause()
        {
            paused = false;
            pauseWait?.Set();
        }

        public void Dispose()
        {
            Cancel();
            tokenSource?.Dispose();
            pauseWait?.Dispose();
            items?.Dispose();
        }
    }
}
