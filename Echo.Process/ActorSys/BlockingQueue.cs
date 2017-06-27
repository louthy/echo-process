using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Echo.ActorSys
{
    public class BlockingQueue<T> : IDisposable
    {
        readonly EventWaitHandle wait = new AutoResetEvent(true);
        readonly object sync = new object();
        volatile bool paused;
        volatile bool cancelled;
        volatile int bufferHead = 0;
        volatile int bufferTail = 0;
        volatile T[] buffer;
        volatile int bufferSize;
        const int InitialBufferSize = 16;

        public readonly int Capacity;

        public bool IsPaused => paused;
        public bool IsCancelled => cancelled;

        public BlockingQueue(int capacity = 100000)
        {
            buffer = new T[InitialBufferSize];
            bufferSize = InitialBufferSize;
            Capacity = capacity;
        }

        public IDisposable ReceiveAsync<S>(S state, Func<S, T, InboxDirective> handler)
        {
            Task.Factory.StartNew(() =>
            {
                var s = state;
                try
                {
                    Receive(msg => handler(s, msg));
                }
                catch (Exception e)
                {
                    Process.logErr(e);
                }
            }, TaskCreationOptions.LongRunning);
            return this;
        }

        public void Receive(Func<T, InboxDirective> handler, string name = "")
        {
            try
            {
                cancelled = false;
                paused = false;

                while (!cancelled)
                {
                    if (bufferTail == bufferHead)
                    {
                        wait.WaitOne();
                        if (cancelled) return;
                    }
                    while (bufferTail != bufferHead)
                    {
                        if (cancelled) return;

                        T item = default(T);
                        var directive = default(InboxDirective);

                        lock (sync)
                        {
                            item = buffer[bufferTail];
                        }

                        try
                        {
                            directive = handler(item);
                        }
                        catch (Exception e)
                        {
                            Process.logErr(e);
                        }

                        if (directive == InboxDirective.Pause)
                        {
                            lock (sync)
                            {
                                buffer[bufferTail] = default(T);
                                bufferTail++;
                                if (bufferTail >= bufferSize) bufferTail = 0;
                            }

                            Pause();
                        }
                        else if (directive == InboxDirective.Shutdown)
                        {
                            Cancel();
                            return;
                        }
                        else
                        {
                            if (directive != InboxDirective.PushToFrontOfQueue)
                            {
                                lock (sync)
                                {
                                    buffer[bufferTail] = default(T);
                                    bufferTail++;
                                    if (bufferTail >= bufferSize) bufferTail = 0;
                                }
                            }
                        }

                        if (cancelled) return;
                        if (paused)
                        {
                            wait.WaitOne();
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Process.logErr(e);
            }
        }

        public int Count => bufferHead >= bufferTail
            ? bufferHead - bufferTail
            : bufferSize - bufferTail + bufferHead;

        public void Post(T value)
        {
            lock (sync)
            {
                var count = Count;
                if (count >= Capacity) throw new QueueFullException();

                if (count < bufferSize)
                {
                    PostToQueue(value);
                }
                else
                {
                    if (Count < bufferSize)
                    {
                        // This protects against a backlog of locks
                        // doubling the buffer unnecessarily.
                        PostToQueue(value);
                        return;
                    }

                    // Create a new buffer that's twice the size of our current one
                    var old = buffer;
                    var oldTail = bufferTail;
                    var newBufferSize = bufferSize <<= 1;
                    buffer = new T[newBufferSize];

                    // Copy the old buffer from the current head position to the end
                    // to the end of the new buffer
                    var endBlockSize = old.Length - bufferHead;
                    var endBlockPos = newBufferSize - endBlockSize;
                    Array.Copy(old, bufferHead, buffer, endBlockPos, endBlockSize);

                    // Set the tail (the last message) to the start of that end block
                    // in the new buffer.  This leaves the head point (where the next 
                    // message will be put) where it is, and therefore we have a new
                    // chunk of empty space to write into.
                    bufferTail = endBlockPos;
                    bufferSize = newBufferSize;

                    // Recall this Post function to add the message
                    PostToQueue(value);
                }
            }
        }

        private void PostToQueue(T value)
        {
            buffer[bufferHead] = value;
            bufferHead++;
            if (bufferHead >= bufferSize)
            {
                bufferHead = 0;
            }
            if (!paused)
            {
                wait.Set();
            }
        }

        public void Cancel()
        {
            cancelled = true;
            bufferHead = 0;
            bufferTail = 0;
            wait.Set();
        }

        public void Pause() =>
            paused = true;

        public void UnPause()
        {
            paused = false;
            wait.Set();
        }

        public void Dispose() =>
            Cancel();
    }
}
