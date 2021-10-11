// The MIT License (MIT)
//
// Copyright (c) 2014 Bryce
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// https://github.com/bryceg/Owin.WebSocket/blob/master/LICENSE

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Owin.WebSocket.Extensions
{
    // Allows serial queuing of Task instances
    // The tasks are not called on the current synchronization context
    public sealed class TaskQueue
    {
        private readonly object mLockObj = new object();
        private Task mLastQueuedTask;
        private volatile bool mDrained;
        private int? mMaxSize;
        private int mSize;

        /// <summary>
        /// Current size of the queue depth
        /// </summary>
        public int Size { get { return mSize; } }

        /// <summary>
        /// Maximum size of the queue depth.  Null = unlimited
        /// </summary>
        public int? MaxSize { get { return mMaxSize; } }
        
        public TaskQueue()
            : this(TaskAsyncHelper.Empty)
        {
        }

        public TaskQueue(Task initialTask)
        {
            mLastQueuedTask = initialTask;
        }

        /// <summary>
        /// Set the maximum size of the Task Queue chained operations.  
        /// When pending send operations limits reached a null Task will be returned from Enqueue
        /// </summary>
        /// <param name="maxSize">Maximum size of the queue</param>
        public void SetMaxQueueSize(int? maxSize)
        {
            mMaxSize = maxSize;
        }

        /// <summary>
        /// Enqueue a new task on the end of the queue
        /// </summary>
        /// <returns>The enqueued Task or NULL if the max size of the queue was reached</returns>
        public Task Enqueue<T>(Func<T, Task> taskFunc, T state)
        {
            // Lock the object for as short amount of time as possible
            lock (mLockObj)
            {
                if (mDrained)
                {
                    return mLastQueuedTask;
                }

                Interlocked.Increment(ref mSize);

                if (mMaxSize != null)
                {
                    // Increment the size if the queue
                    if (mSize > mMaxSize)
                    {
                        Interlocked.Decrement(ref mSize);

                        // We failed to enqueue because the size limit was reached
                        return null;
                    }
                }

                var newTask = mLastQueuedTask.Then((next, nextState) =>
                {
                    return next(nextState).Finally(s =>
                    {
                        var queue = (TaskQueue)s;
                        Interlocked.Decrement(ref queue.mSize);
                    },
                    this);
                },
                taskFunc, state);

                mLastQueuedTask = newTask;
                return newTask;
            }
        }

        /// <summary>
        /// Triggers a drain fo the task queue and blocks until the drain completes
        /// </summary>
        public void Drain()
        {
            lock (mLockObj)
            {
                mDrained = true;

                mLastQueuedTask.Wait();

                mDrained = false;
            }
        }
    }
}