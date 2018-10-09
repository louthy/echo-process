using System;
using System.Collections.Generic;
using Xunit;
using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;
using static Echo.ProcessConfig;
using static Echo.Strategy;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Echo.Tests
{
    public class LifeTimeTests
    {
        public class ProcessFixture : IDisposable
        {
            public ProcessFixture()
            {
                initialise();
                subscribe<Exception>(Errors(), e => raise<Unit>(e));
            }

            public void Dispose() => shutdownAll();
        }


        public class LifeTimeIssues : IClassFixture<ProcessFixture>
        {
            private static void WaitForActor(ProcessId pid)
            {
                using (var mre = new ManualResetEvent(false))
                {
                    using (observeStateUnsafe<Unit>(pid).Subscribe(_ => { }, () => mre.Set()))
                    {
                        mre.WaitOne();
                    }

                    // actor is in shutdown, wait a very short time
                    while (exists(pid))
                    {
                        Task.Delay(1).Wait();
                    }
                }
            }

            [Fact(Timeout = 5000)]
            // make sure that the state is not re-initialized after kill
            // issue 48 (race condition resulted in inboxFn called after DisposeState)
            public void NoZombieState()
            {
                var events = new List<string>();
                var actor = spawn<IDisposable, string>(nameof(NoZombieState),
                    () =>
                    {
                        events.Add("setup");
                        return Disposable.Create(
                            () => events.Add("dispose")
                        );
                    },
                    (state, msg) =>
                    {
                        events.Add(msg);
                        Task.Delay(100 * milliseconds).Wait();
                        Assert.Equal(1, Process.inboxCount(Self)); // msg inbox2 will arrive before kill is executed
                        kill();
                        return state;
                    });
                tell(actor, "inbox1"); // inbox1 might arrive before StartUp-System-Message (but doesn't matter)
                tell(actor, "inbox2"); // msg inbox2 will arrive before kill is executed
                WaitForActor(actor);
                Assert.False(Process.exists(actor));
                Assert.Equal(events.Freeze(), List("setup", "inbox1", "dispose"));
            }


            [Fact(Timeout = 5000)]
            // issue 47 / pr 49
            public void ActorWithoutCancellationToken()
            {
                int inboxResult = 0;

                var actor = spawn(nameof(ActorWithoutCancellationToken), (string msg) =>
                {
                    Task.Delay(100 * milliseconds).Wait();
                    inboxResult++;
                });
                tell(actor, "test");
                kill(actor);
                Assert.Equal(1, inboxResult);
            }

            [Fact(Timeout = 5000)]
            // issue 47 / pr 49
            public void ActorWithCancellationToken()
            {
                int inboxResult = 0;

                var actor = spawn(nameof(ActorWithCancellationToken), (string msg) =>
                {
                    Task.Delay(100 * milliseconds).Wait(SelfProcessCancellationToken);
                    if (!SelfProcessCancellationToken.IsCancellationRequested) inboxResult++;
                });
                tell(actor, "test");
                kill(actor);
                Assert.Equal(0, inboxResult);
            }
        }
    }
}
