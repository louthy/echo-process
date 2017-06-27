using Echo;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Scratchpad
{
    public static class PausableBlockingQueueTests
    {
        public static void Run()
        {

            var errors = Seq<ProcessLogItem>();

            int count = 0;
            var queue = new PausableBlockingQueue<int>(100000);

            const int max = 1000;

            using (queue.ReceiveAsync(0, (s, m) => { count++; return InboxDirective.Default; }))
            {
                Range(0, max).Iter(i => queue.Post(i));

                Console.WriteLine("*** All Posted ***");
                Console.ReadKey();

                if (count == max)
                {
                    Console.WriteLine($"Should be {max} is actually {count} (errors: {errors.Count})");
                }
                else
                {
                    Console.WriteLine($"Should be {max} is actually {count} (errors: {errors.Count})");
                }

                Console.WriteLine("Press any key to quit");
                Console.ReadKey();
            }
        }
    }

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
            Task.Factory.StartNew(() =>
            {
                bool addToFrontOfQueue = false;

                foreach (var item in items.GetConsumingEnumerable(token))
                {
                    do
                    {
                        if (token.IsCancellationRequested) return;

                        if (IsPaused)
                        {
                            pauseWait?.WaitOne();
                        }

                        var directive = handler(state, item);

                        switch (directive)
                        {
                            case InboxDirective.Default:

                                break;

                            case InboxDirective.Pause:
                                Pause();
                                break;

                            case InboxDirective.Shutdown:
                                Cancel();
                                return;

                            case InboxDirective.PushToFrontOfQueue:
                                addToFrontOfQueue = true;
                                break;
                        }
                    }
                    while (addToFrontOfQueue);
                }
            }, token, TaskCreationOptions.LongRunning,TaskScheduler.Default);

        public void Post(A value)
        {
            if(!items.TryAdd(value))
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
