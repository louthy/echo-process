using System;
using Echo;
using Echo.ActorSys;
using LanguageExt;
using static LanguageExt.Prelude;
using Xunit;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Threading;

namespace Echo.Tests
{
    public class BlockingQueueTests
    {
        static volatile int count = 0;

        [Fact]
        public void TestTypes()
        {
            var errors = Seq<ProcessLogItem>();

            Process.ProcessSystemLog
                   .Where(log => log.Type > ProcessLogItemType.Warning)
                   .Subscribe(log => errors = log.Cons(errors));

            var queue = new PausableBlockingQueue<int>(10000);

            const int max = 1000;

            using (queue.ReceiveAsync(0, (s, m) => { count++; return InboxDirective.Default; }))
            {
                Range(0, max).Iter(i => queue.Post(i));

                Thread.Sleep(1000);

                Assert.True(count == max, $"Should be {max} is actually {count} (errors: {errors.Count})");
            }
        }
    }
}
