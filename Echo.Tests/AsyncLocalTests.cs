using LanguageExt;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Echo.Process;
using static Echo.ProcessConfig;

namespace Echo.Tests
{
    public class AsyncLocalTests
    {
        /// <summary>
        /// Test basic single threaded set session
        /// </summary>
        [Fact]
        public void SetSession()
        {
            Assert.True(sessionId().IsNone);

            var sid = SessionId.Generate();
            setSession(sid);

            Assert.True(sessionId() == sid);
        }


        /// <summary>
        /// Assert that session ids are truly 'ThreadStatic'
        /// </summary>
        [Fact]
        public void SetSessionMultiThreaded()
        {
            Assert.True(sessionId().IsNone);

            ThreadPool.GetMinThreads(out var origMinWorkerThreads, out var origMinPortThreads);
            ThreadPool.GetMaxThreads(out var origMaxWorkerThreads, out var origMaxPortThreads);
            try
            {
                ThreadPool.SetMinThreads(2, 2);
                ThreadPool.SetMaxThreads(2, 2);

                var tasks = new List<Task>();
                

                for (int i = 0; i < 10; i++)
                {
                    var index = i;
                    tasks.Add(Task.Run(() => SetSession()));
                }

                Task.WaitAll(tasks.ToArray());

                //assert that our current thread's session is still none.
                Assert.True(sessionId().IsNone);
            }
            finally
            {
                ThreadPool.SetMinThreads(origMinWorkerThreads, origMinPortThreads);
                ThreadPool.SetMaxThreads(origMaxWorkerThreads, origMaxPortThreads);
            }
        }

        /// <summary>
        /// Check if session id is propagated to child tasks but not sent upstream
        /// </summary>
        [Fact]
        public void SetChildSession()
        {
            Assert.True(sessionId().IsNone);

            var sid = SessionId.Generate();
            setSession(sid);

            Assert.True(sessionId() == sid);

            //start child task

            var t = Task.Run(() =>
            {
                Assert.True(sessionId() == sid);


                var sid2 = SessionId.Generate();
                setSession(sid2);

                Assert.True(sessionId() == sid2);

            });
            Task.WaitAll(t);

            //make sure that our session id is still same
            Assert.True(sessionId() == sid);
        }
    }
}
