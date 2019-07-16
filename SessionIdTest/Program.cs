using Echo;
using System;
using LanguageExt;
using static Echo.Process;
using static Echo.ProcessConfig;
using static LanguageExt.Prelude;

namespace SessionIdTest
{
    /// <summary>
    /// This test app shows how you can use your own session keys with the Process system
    /// By calling withSession you create a context where the session ID is set.  If you
    /// tell or ask a message during that time the session ID will travel with the messages.
    /// 
    /// If a Process receives a message with a session ID attached, then the session ID will
    /// be in context for the duration of the Process's message function.
    /// 
    /// NOTE: This is side-stepping the built-in session management system, and is simply 
    /// moving session IDs around with the messages.  If you want full session management
    /// use sessionStart.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            RedisCluster.register();
            initialise("app", "session-id-role", "session-id-node-1", "localhost", "0");

            var app = spawn<Unit, string>(
                Name: "application",
                Setup: ()       => ignore(spawn<string>("child", msg => Console.WriteLine($"{msg} : {sessionId()}"), ProcessFlags.PersistInbox)),
                Inbox: (_, msg) => fwdChild("child"),
                Flags: ProcessFlags.PersistInbox);

            while (true)
            {
                var sid = SessionId.Generate();

                withSession(sid, () =>
                {
                    sessionId();
                    tell(app, "Hello");
                });

                if(Console.ReadKey().Key == ConsoleKey.Escape)
                {
                    break;
                }
            }
        }
    }
}
