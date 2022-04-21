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
                        Assert.Equal(1, inboxCount(Self)); // msg inbox2 will arrive before kill is executed
                        kill();
                        return state; // will never get here
                    });
                tell(actor, "inbox1"); // inbox1 might arrive before StartUp-System-Message (but doesn't matter)
                tell(actor, "inbox2"); // msg inbox2 will arrive before kill is executed
                WaitForActor(actor);
                Assert.False(Process.exists(actor));
                Assert.Equal(List("setup", "inbox1", "dispose"), events.Freeze());
            }

            [Fact(Timeout = 5000)]
            // make sure that the state is not re-initialized after kill and that ask is processed cleanly (sync) until kill
            public void NoZombieStateWhenAsked()
            {
                var events = new List<string>();
                var actor = spawn<IDisposable, string>(nameof(NoZombieStateWhenAsked),
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
                        Assert.Equal(1, inboxCount(Self)); // msg 2 will arrive before kill is executed
                        reply($"{msg}answer");
                        events.Add($"{msg}answer");
                        return state;
                    });
                var answer1Task = Task.Run(() => ask<string>(actor, "request1")); // request will arrive before kill is executed
                var answer2task = Task.Run(() => ask<string>(actor, "request2")); // request will arrive before kill is executed
                Task.Delay(50 * milliseconds).Wait();
                kill(actor);
                WaitForActor(actor);
                Assert.False(Process.exists(actor));
                Assert.Equal("request1answer", answer1Task.Result);
                Assert.Equal(List("setup", "request1", "request1answer", "dispose"), events.Freeze());
                Assert.Equal("ProcessException", Try(() => answer2task.Result).IfFail().With<AggregateException>(_ => _.InnerException.GetType().Name).OtherwiseReThrow());
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
                Task.Delay(50).Wait();
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
                    if (!SelfProcessCancellationToken.IsCancellationRequested)
                    {
                        // will never get here
                        inboxResult++;
                    }
                });
                tell(actor, "test");
                Task.Delay(50).Wait();
                kill(actor);
                Assert.Equal(0, inboxResult);
            }

            [Fact(Timeout = 35000)]
            public void TimeoutDoesNotKillAskInboxState()
            {
                /* The idea is that one 'ask' fails on TimeoutException and this
                 * does not affect the second 'ask' that is processing the message
                 * while the first 'ask' failed.
                 *
                 * The problem was that the 'ask actor' state was lost because
                 * there was an ObjectDisposedException in it's inbox.
                 */
                
                var slow = spawn<Unit>(
                    $"slow",
                    _ =>
                    {
                        Task.Delay(31000).Wait();
                        reply(unit);
                    });

                var quick = spawn<Unit>(
                    $"quick",
                    _ =>
                    {
                        Task.Delay(5000).Wait();
                        reply(true);
                    });

                var askSlow = spawn<Unit>(
                    $"ask-slow",
                    _ => ask<Unit>(slow, unit));


                var askedFine = false;
                var askQuick = spawn<Unit>(
                    $"ask-quicker",
                    _ => askedFine = ask<bool>(quick, unit));

                // Asking the slow process...that one will fail in 30s timeout - that is expected
                tell(askSlow, unit);
                // "Waiting 28s
                Thread.Sleep(28000);
                // "After 28s asking the quicker process - that one should reply within 5s (the slow one times out in the meanwhile).
                tell(askQuick, unit);
                // "Waiting 6s
                Thread.Sleep(6000);
                // "The TimeoutException in the slow process should not affect the quick process, so we should have 'done' here.
                Assert.True(askedFine);
            }
        }
    }
}
