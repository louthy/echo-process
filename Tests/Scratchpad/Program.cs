using System;
using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;
using Echo;

namespace Scratchpad
{
    class Program
    {
        private static void SimpleTest()
        {
            var pid = spawn<Option<DateTime>, string>("watcher", () =>
            {
                Console.WriteLine("Setup process");
                return None;
            }
            , (state, msg) =>
            {
                Console.WriteLine("Received " + msg);
                return DateTime.UtcNow;
            });

            Console.WriteLine($"process {pid} spawned");

            pid.Tell("hello world");
        }

        static void Main(string[] args)
        {
            ProcessConfig.initialise();

            SimpleTest();

            Console.ReadKey();

            shutdownAll();

            Console.WriteLine("end, press key.");
            Console.ReadKey();

        }
    }
}
