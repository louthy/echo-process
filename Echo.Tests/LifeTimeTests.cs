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

        [Collection("no-parallelism")]
        public class LifeTimeIssues : IClassFixture<ProcessFixture>
        {
            private static void WaitForActor(ProcessId pid)
            {
                using (var mre = new ManualResetEvent(false))
                {
                    using (observeStateUnsafe<Unit>(pid).Subscribe(_ => { }, () => mre.Set()))
                    {
                        if (mre.WaitOne(5000))
                        {
                            throw new TimeoutException($"State not changed within 10s: {pid}");
                        }
                    }

                    var start = DateTime.Now;
                    // actor is in shutdown, wait a very short time
                    while (exists(pid))
                    {
                        if ((DateTime.Now - start).TotalSeconds > 5)
                        {
                            throw new TimeoutException($"PID still exists after 10s: {pid}");
                        }

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

            /*[Fact(Timeout = 5000)]
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
            }*/

            [Fact(Timeout = 5000)]
            public void KillAndStart()
            {
                static ProcessId start(int state) =>
                    spawn<int, Unit>(
                        nameof(KillAndStart),
                        () => state,
                        (int state, Unit _) =>
                        {
                            reply(state);
                            return state;
                        });

                // start and check it is running with a correct startup number
                var actor = start(1);
                Assert.Equal(1, ask<int>(actor, unit));
                Assert.True(children(User()).Contains(actor));
                
                // kill and make sure it is gone
                kill(actor);
                WaitForActor(actor);
                Assert.False(children(User()).Contains(actor));
                
                // start and check it is running with a correct startup number
                actor = start(2);
                Assert.Equal(2, ask<int>(actor, unit));
                Assert.True(children(User()).Contains(actor));
                
                kill(actor);
            }

            [Fact(Timeout = 5000)]
            public void KillAndStartChild()
            {
                var actor = spawn(nameof(KillAndStartChild), (string msg) =>
                {
                    if (msg == "count")
                    {
                        reply(Children.Count.ToString());
                        return;
                    }
                    else
                    {
                        var child = Children.Find("child")
                                            .IfNone(() => spawn("child", (string msg) => reply(msg)));
                        if (msg == "kill")
                        {
                            kill(child);
                            WaitForActor(child);
                        }
                        else
                        {
                            fwd(child);
                        }
                    }
                });
                
                Assert.Equal("0", ask<string>(actor, "count"));
                Assert.Equal("test1", ask<string>(actor, "test1"));
                Assert.Equal("1", ask<string>(actor, "count"));
                
                tell(actor, "kill");
                
                Assert.Equal("0", ask<string>(actor, "count"));
                Assert.Equal("test2", ask<string>(actor, "test2"));
                Assert.Equal("1", ask<string>(actor, "count"));
                
                kill(actor);
            }
                        
            [Theory]
            [InlineData(false, 1)]
            [InlineData(true, 3)]
            public void CrashAndManageState(bool resume, int expectedState)
            {
                var actor = spawn<int>(
                    nameof(CrashAndManageState),
                    (int msg) =>
                    {
                        var child = Children.Find("child")
                                            .IfNone(() => spawn<int, int>(
                                                        "child",
                                                        () => 0,
                                                        (state, msg) =>
                                                        {
                                                            if (msg == 2) throw new Exception();
                                                            reply(state + msg);
                                                            return state + msg;
                                                        }));
                        fwd(child);
                    },
                    Strategy: OneForOne(Always(resume ? Directive.Resume : Directive.Restart)));

                Assert.Equal(1, ask<int>(actor, 1));
                Assert.Equal(2, ask<int>(actor, 1));

                tell(actor, 2); // crash + resume/restart
                Task.Delay(100).Wait();
                Assert.Equal(expectedState, ask<int>(actor, 1));

                kill(actor);
            }
        }
    }
}
