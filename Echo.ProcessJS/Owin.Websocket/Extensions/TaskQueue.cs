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