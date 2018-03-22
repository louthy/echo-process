using Echo;
using static Echo.Process;
using static Echo.ProcessConfig;
using LanguageExt;
using static LanguageExt.Prelude;
using System;

namespace ScheduledMessages
{
    class Program
    {
        const string scheduler = "loop-schedule";

        static void Main(string[] args)
        {
            RedisCluster.register();
            initialise("app", "schedule-test", "schedule-test1", "localhost", "0");
            var pid = spawn<int>("loop", Inbox, ProcessFlags.PersistInbox);

            while (true)
            {
                var key = Console.ReadKey();
                if(key.KeyChar >= '0' && key.KeyChar <='9' )
                {
                    tell(pid, key.KeyChar - '0');
                }
                else
                {
                    return;
                }
            }
        }

        static void Inbox(int x)
        {
            Console.WriteLine(x);
            //tellSelf(x + 1, Schedule.Ephemeral(250 * ms, scheduler));
            tellSelf(x + 1, Schedule.Persistent(250 * ms, scheduler));
        }
    }
}
