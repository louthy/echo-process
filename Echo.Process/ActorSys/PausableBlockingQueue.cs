using Echo;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys
{
    public class PausableChannel<A> : IDisposable
    {
        readonly Channel<A> channel;
        readonly Func<A, ValueTask<InboxDirective>> consumer;
        CancellationTokenSource tokenSource;
        CancellationToken token;
        IDisposable task;
        volatile bool paused;

        public PausableChannel(int boundedCapacity, Func<A, ValueTask<InboxDirective>> consumer)
        {
            boundedCapacity = boundedCapacity < 1 ? 100000 : boundedCapacity;
            var opts = new BoundedChannelOptions(boundedCapacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleWriter = false,
                SingleReader = true
            };

            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;
            channel = Channel.CreateBounded<A>(opts);
            task = Task.Factory.StartNew(Receive, token);
        }

        async Task Receive() 
        {
            while (!token.IsCancellationRequested && await channel.Reader.WaitToReadAsync(token))
            {
                var item = await channel.Reader.ReadAsync(token);

                bool addToFrontOfQueue = false;
                var directive = default(InboxDirective);

                do
                {
                    try
                    {
                        directive = await consumer(item);
                    }
                    catch (Exception e)
                    {
                        Process.logErr(e);
                    }

                    if (directive.HasFlag(InboxDirective.Shutdown))
                    {
                        addToFrontOfQueue = false;
                        Cancel();
                    }
                    else
                    {
                        addToFrontOfQueue = directive.HasFlag(InboxDirective.PushToFrontOfQueue);

                        if (directive.HasFlag(InboxDirective.Pause))
                        {
                            Pause();
                        }
                    }
                } while (addToFrontOfQueue && !paused);
            }
        }
    
        public void Pause()
        {
            // Pause
            paused = true;
 
            // Cancel the task
            tokenSource?.Cancel();

            // Dispose the task
            var ltask = task;
            task = null;
            ltask?.Dispose();
        }

        public void UnPause()
        {
            // Unpause
            paused = false;
            
            // Restart the task
            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;
            task = Task.Factory.StartNew(Receive, token);
        }        
        
        public void Cancel()
        {
            // Cancel the task
            tokenSource?.Cancel();
 
            // Unpause
            paused = false;

            // Dispose the task
            var ltask = task;
            task = null;
            ltask?.Dispose();
        }
        
        public void Dispose()
        {
            Cancel();
        }
    }
    
    
    internal class PausableBlockingQueue_OLD<A> : IDisposable
    {
        readonly EventWaitHandle pauseWait = new AutoResetEvent(false);
        readonly BlockingCollection<A> items;
        readonly CancellationTokenSource tokenSource;
        readonly CancellationToken token;

        volatile bool paused;
        public bool IsPaused => paused;
        public bool IsCancelled => token.IsCancellationRequested;

        public PausableBlockingQueue_OLD(int boundedCapacity)
        {
            boundedCapacity = boundedCapacity < 1 ? 100000 : boundedCapacity;
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
                var directive = default(InboxDirective);

                if (token.IsCancellationRequested) return;

                try
                {
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

                            if (directive.HasFlag(InboxDirective.Shutdown))
                            {
                                addToFrontOfQueue = false;
                                Cancel();
                            }
                            else
                            {
                                addToFrontOfQueue = directive.HasFlag(InboxDirective.PushToFrontOfQueue);

                                if (directive.HasFlag(InboxDirective.Pause))
                                {
                                    Pause();
                                }
                            }
                        }
                        while (addToFrontOfQueue);
                    }
                }
                catch(OperationCanceledException)
                {
                    return;
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
