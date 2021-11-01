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
            static Unit WaitForKill(ProcessId pid, int timeOutSeconds = 3)
            {
                var total = timeOutSeconds * 1000;
                var span  = 200;
                while (total > 0)
                {
                    if (!Process.exists(pid)) return unit;
                    Task.Delay(span).Wait();
                    total -= span;
                }
                throw new TimeoutException($"Process still exists after {timeOutSeconds}s: {pid}");
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
                        return Disposable.Create(() => events.Add("dispose"));
                    },
                    (state, msg) =>
                    {
                        events.Add(msg);
                        Task.Delay(100 * milliseconds).Wait();
                        kill();
                        return state; // will never get here
                    });
                tell(actor, "inbox1"); // inbox1 might arrive before StartUp-System-Message (but doesn't matter)
                tell(actor, "inbox2"); // msg inbox2 may or may not arrive before kill is executed, either way, it shouldn't be processed
                WaitForKill(actor);
                
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
                        return Disposable.Create(() => events.Add("dispose"));
                    },
                    (state, msg) =>
                    {
                        events.Add(msg);
                        reply($"{msg}answer");
                        events.Add($"{msg}answer");
                        return state;
                    });
                var answer1Task = Task.Run(() => ask<string>(actor, "request1")); // request will arrive before kill is executed
                Task.Delay(50).Wait();

                kill(actor);
                Task.Delay(50).Wait();
 
                var answer2task = Task.Run(() => ask<string>(actor, "request2")); // request will arrive after kill is executed
                Task.Delay(50).Wait();

                WaitForKill(actor);
                Assert.Equal("request1answer", answer1Task.Result);
                Assert.Equal(List("setup", "request1", "request1answer", "dispose"), events.Freeze());
                Assert.Throws<AggregateException>(() => answer2task.Result);
            }

            [Fact(Timeout = 5000)]
            // issue 47 / pr 49
            public void ActorWithoutCancellationToken()
            {
                int inboxResult = 0;

                var actor = spawn(nameof(ActorWithoutCancellationToken), (string msg) =>
                {
                    inboxResult++;
                });
                tell(actor, "test");
                Task.Delay(50).Wait();
                kill(actor);
                Assert.Equal(1, inboxResult);
            }

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
                WaitForKill(actor);
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
                ProcessId child = ProcessId.None;
                var actor = spawn(nameof(KillAndStartChild), (string msg) =>
                {
                    if (msg == "count")
                    {
                        reply(Children.Count.ToString());
                        return;
                    }
                    else
                    {
                        child = Children.Find("child")
                                        .IfNone(() => spawn("child", (string msg) => reply(msg)));
                        if (msg == "kill")
                        {
                            kill(child);
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
                WaitForKill(child);
                
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
                ProcessId spawnKidIfNotExists() =>
                    spawn<int, int>(
                        "child",
                        () => 0,
                        (state, msg) => {
                            if (msg == 2) throw new Exception();
                            reply(state + msg);
                            return state + msg;
                        });
                
                var actor = spawn<int>(
                    $"{nameof(CrashAndManageState)}{resume}{expectedState}",
                    (int msg) => {
                        fwd(Children.Find("child").IfNone(spawnKidIfNotExists));
                    },
                    Strategy: OneForOne(Always(resume ? Directive.Resume : Directive.Restart)));

                Assert.Equal(1, ask<int>(actor, 1));
                Assert.Equal(2, ask<int>(actor, 1));

                tell(actor, 2); // crash + resume/restart
                Task.Delay(100).Wait();
                Assert.Equal(expectedState, ask<int>(actor, 1));

                kill(actor);
            }
            
            [Fact(Timeout = 5000)]
            public void RestartWaitsForMessageToComplete()
            {
                int threadsCount = 0;
                var messages     = Seq<string>();
                
                var actor = spawn(nameof(RestartWaitsForMessageToComplete), (string msg) =>
                {
                    threadsCount++;
                    messages = messages.Add(msg);
                    Task.Delay(100 * milliseconds).Wait();
                    threadsCount--;
                });
                
                tell(actor, "test");
                Task.Delay(20).Wait();
                Assert.Equal(1, threadsCount);
                Assert.Equal(Seq1("test"), messages);
                
                restart(actor);
                Task.Delay(20).Wait();
                Assert.Equal(1, threadsCount); // still processing the first message
                Assert.Equal(Seq1("test"), messages);
                
                Task.Delay(80).Wait();
                Assert.Equal(0, threadsCount);        // finished processing  
                Assert.Equal(Seq1("test"), messages); // only one message was in the queue
                
            }

            [Fact(Timeout = 5000)]
            public void RestartWaitsForMessageToCompleteBeforeStartingNext()
            {
                int threadsCount = 0;
                var messages     = Seq<string>();
                
                var actor = spawn(nameof(RestartWaitsForMessageToCompleteBeforeStartingNext), (string msg) =>
                {
                    threadsCount++;
                    messages = messages.Add(msg);
                    Task.Delay(100 * milliseconds).Wait();
                    threadsCount--;
                });
                
                tell(actor, "test1");
                Task.Delay(20).Wait();
                Assert.Equal(1, threadsCount);
                Assert.Equal(Seq1("test1"), messages);
                
                restart(actor);
                tell(actor, "test2");
                Task.Delay(20).Wait();
                Assert.Equal(1, threadsCount); // finishing the first message
                Assert.Equal(Seq1("test1"), messages);
                
                Task.Delay(80).Wait();
                Assert.Equal(1, threadsCount); // processing the second message  
                Assert.Equal(Seq("test1", "test2"), messages);
                
            }
            
              [Fact(Timeout = 5000)]
            public void KillCompletesTheMessage()
            {
                int threadsCount = 0;
                var messages     = Seq<string>();
                
                var actor = spawn(nameof(KillCompletesTheMessage), (string msg) =>
                {
                    threadsCount++;
                    messages = messages.Add(msg);
                    Task.Delay(100 * milliseconds).Wait();
                    threadsCount--;
                });
                
                tell(actor, "test");
                Task.Delay(20).Wait();
                Assert.Equal(1, threadsCount); // keep processing the message
                Assert.Equal(Seq1("test"), messages);
                
                var start = DateTime.Now;
                kill(actor);
                Assert.True((DateTime.Now - start).TotalMilliseconds < 50); // kill is async and will finish in next 70ms
                Task.Delay(20).Wait();
                Assert.Equal(1, threadsCount); // keep processing the message
                Assert.Equal(Seq1("test"), messages);
                
                Task.Delay(80).Wait();
                Assert.Equal(0, threadsCount); // message gone, process gone
                Assert.False(exists(actor));
                Assert.Equal(Seq1("test"), messages); 
                
            }
            
            [Fact]
            public void KillDoesNotWaitForAllMessages()
            {
                var  messages    = Seq<int>();
                bool wasShutdown = false;

                var actor = spawn(
                    nameof(KillDoesNotWaitForAllMessages),
                    (int msg) =>
                    {
                        messages = messages.Add(msg);
                        Task.Delay(100 * milliseconds).Wait();
                    },
                    Shutdown: () => ignore(wasShutdown = true));

                for (int i = 1; i <= 10; i++)
                {
                    tell(actor, i);    
                }
                
                Task.Delay(20).Wait();
                kill(actor); // kill sent while the first message is being processed
                
                for (int i = 11; i <= 20; i++)
                {
                    tell(actor, i);    
                }
                
                WaitForKill(actor);
                
                // The inbox should die after the first message
                Assert.True(Seq1(1) == messages, $"Only the first message should be processed. Actual: {messages.ToFullString()}. Shutdown called: {wasShutdown}"); 
            }
                        
            [Fact]
            public void SetupIsCalledOnlyOnce()
            {
                var setupCount = 0;
                var actor = spawn<int, string>(
                    nameof(SetupIsCalledOnlyOnce),
                    () =>
                    {
                        setupCount++;
                        Task.Delay(30).Wait();
                        return 0;
                    },
                    (state, msg) => state);
                
                tell(actor, "test1");
                tell(actor, "test2");
                tell(actor, "test3");
                tell(actor, "test4");
                Task.Delay(100).Wait();
                
                Assert.Equal(1, setupCount);
            }
        }
    }
}
