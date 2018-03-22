using Echo;
using static Echo.Process;
using static Echo.ProcessConfig;
using LanguageExt;
using static LanguageExt.Prelude;
using System;
using System.Collections.Generic;
using System.Linq;
using LanguageExt.ClassInstances;

namespace ScheduledMessages
{
    class Program
    {
        const string scheduler = "loop-schedule";

        static void Main(string[] args)
        {
            RedisCluster.register();
            initialise("app", "schedule-test", "schedule-test1", "localhost", "0");
            var pid = spawn<Lst<int>>("loop", InboxAppend, ProcessFlags.PersistInbox);

            while (true)
            {
                var key = Console.ReadKey();
                if(key.KeyChar >= '0' && key.KeyChar <='9' )
                {
                    tell(pid, List(key.KeyChar - '0'));
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

        static void InboxAppend(Lst<int> many)
        {
            Console.WriteLine(String.Join(", ", many.Map(toString)));

            tellSelf(List(many.Count + 1), Schedule.PersistentAppend<MLst<int>, Lst<int>>(5 * s, scheduler));
            tellSelf(List(many.Count + 2), Schedule.PersistentAppend<MLst<int>, Lst<int>>(5 * s, scheduler));
            tellSelf(List(many.Count + 3), Schedule.PersistentAppend<MLst<int>, Lst<int>>(5 * s, scheduler));
            tellSelf(List(many.Count + 4), Schedule.PersistentAppend<MLst<int>, Lst<int>>(5 * s, scheduler));
            tellSelf(List(many.Count + 5), Schedule.PersistentAppend<MLst<int>, Lst<int>>(5 * s, scheduler));
        }
    }
}
