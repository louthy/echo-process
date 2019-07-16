using Echo;
using System;

namespace Caching
{
    class Program
    {
        static void Main(string[] args)
        {
            ProcessConfig.initialise();

            Process.DeadLetters()
                   .Observe<DeadLetter>()
                   .Subscribe(Console.WriteLine);

            Process.ProcessSystemLog
                   .Subscribe(Console.WriteLine);

            FuncCaching.Run();

            ClassCaching.Run();

            Console.ReadKey();
        }
    }
}