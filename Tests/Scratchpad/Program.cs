using System;
using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;
using Echo;

namespace Scratchpad
{
    public interface IMyState : IDisposable
    {
        void Add(int num);
        void Remove(int num);
        void Show();

    }
    class Program
    {
        static void Main(string[] args)
        {
            ProcessConfig.initialise();

            ProxyTest();

            Console.ReadKey();

            shutdownAll();

            Console.WriteLine("end, press key.");
            Console.ReadKey();

        }

        public class MyState : IMyState
        {
            HashSet<int> state = HashSet<int>();

            public MyState()
            {
                Console.WriteLine("Setup called");
            }

            public void Add(int num)
            {
                Console.WriteLine($"add {num}...");
                state = state.Add(num);
            }

            public void Dispose()
            {
                Console.WriteLine($"Dispose");
            }

            public void Remove(int num)
            {
                Console.WriteLine($"Remove {num}...");
                state = state.Remove(num);
            }

            public void Show()
            {
                Console.WriteLine("content: " + string.Join(",", state));
            }
        }

        private static void ProxyTest()
        {
            IMyState state = spawn<IMyState>("mystate", () => new MyState());

            state.Add(5);
            state.Add(3);
            state.Show();
            state.Remove(5);
            state.Show();
        }
    }
}
