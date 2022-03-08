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
using LanguageExt.UnitsOfMeasure;

namespace Echo.Tests
{
    public class StrategyTests
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

        public class StrategyStateProperties : IClassFixture<ProcessFixture>
        {
            [Fact]
            public void FirstFailureTime()
            {
                string err = "";
                var start = DateTimeOffset.Now;
                State<StrategyContext, Unit> MyStrategy() =>
                    from context in Context
                    let _1 = err += $"{context.Global.Failures}:f={(context.Global.FirstFailure < DateTime.MaxValue ? Math.Round((context.Global.FirstFailure - start).TotalSeconds) : -1)},"
                    let _2 = err += $"l={(context.Global.FirstFailure < DateTime.MaxValue ? Math.Round((context.Global.LastFailure - start).TotalSeconds) : -1)}|"
                    from x in Compose(SetBackOffAmount(1000 * milliseconds), SetDirective(Directive.Resume), Redirect(MessageDirective.StayInQueue))
                    select x;

                var actor = spawn<Unit, string>(nameof(FirstFailureTime), () => ignore(spawn("sub", (string msg) =>
                {

                    throw new Exception();
                    err += $"{DateTimeOffset.Now}: {msg}\n";
                })), (dummyUnit, _) => tellChild("sub", _), Strategy: MyStrategy());

                tell(actor, "test");
                Task.Delay(2500).Wait();
                kill(actor);

                Assert.Equal("1:f=-1,l=-1|2:f=0,l=0|3:f=0,l=1|", err);
            }
        }

        public class StrategyRestartDelayIssue : IClassFixture<ProcessFixture>
        {
            [Fact(Timeout = 10000)]
            // issue #57 (Fix strategy restart unpause timing)
            public static void DelayMissing()
            {
                var events = new List<(Time, string)>();
                using (var mre = new ManualResetEvent(false))
                {
                    var begin = DateTimeOffset.MinValue;
                    int setupCallCounter = 0;
                    int inboxCallCounter = 0;
                    var outer = spawn<Unit, string>(nameof(DelayMissing) + "-wrapper", () =>
                    {
                        spawn<Unit, string>(nameof(DelayMissing),
                            () =>
                            {

                                setupCallCounter++;
                                if (setupCallCounter == 1) begin = DateTimeOffset.Now; // set begin here to make timing more precise
                                events.Add((DateTimeOffset.Now - begin, $"setup {setupCallCounter}"));
                                // Console.WriteLine($"{DateTimeOffset.Now:O} setup {setupCallCounter}");
                                if (setupCallCounter > 1) throw new Exception($"{DateTimeOffset.Now} setup {setupCallCounter} fails");
                                SelfProcessCancellationToken.Register(() => mre.Set());
                                return unit;
                            },
                            (state, msg) =>
                            {
                                inboxCallCounter++;
                                events.Add((DateTimeOffset.Now - begin, $"inbox {inboxCallCounter}"));
                                // Console.WriteLine($"{DateTimeOffset.Now:O} inbox {inboxCallCounter}");
                                throw new Exception($"{DateTimeOffset.Now} inbox {inboxCallCounter} fails");
                            });
                        return unit;
                    }, (state, msg) =>
                    {
                        reply(msg.ToUpper());
                        return state;
                    }, Strategy: from x in Context
                    from y in x.Exception is ProcessSetupException 
                        ? Compose(SetBackOffAmount(0.1 * second), Redirect(MessageDirective.StayInQueue), SetDirective(x.Global.Failures < 3 ? Directive.Escalate : Directive.Restart))
                        : x.Global.Failures < 2
                            ? Compose(SetBackOffAmount(0.1 * second), SetDirective(Directive.Restart), Redirect(MessageDirective.StayInQueue))
                            : SetDirective(Directive.Stop)
                    select y);

                var answer = ask<string>(outer, "test");
                Assert.Equal("TEST", answer); // make sure outer actor is running
                tell(outer[nameof(DelayMissing)], "TEST");
                    mre.WaitOne();
                    // Task.Delay(1000).ConfigureAwait(false).GetAwaiter().GetResult();
                }

                var expected = List(
                    (0 * second, "setup 1"),
                    (0 * second, "inbox 1"),
                    (0.1 * second, "setup 2")
                    );
                Assert.Equal(expected.Map(_ => _.Item2), events.Map(_ => _.Item2));
                foreach (var timing in expected.Zip(events.Map(_ => _.Item1)))
                {
                    Assert.True((timing.Item2 - timing.Item1.Item1).Abs() < 0.3 * seconds, $"Timing different: {timing.Item1.Item2} expected {timing.Item1.Item1}, but was {timing.Item2}");
                }
            }

        }
    }
}
