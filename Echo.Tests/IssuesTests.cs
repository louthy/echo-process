using System;
using System.Collections.Generic;
using Xunit;
using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;
using static Echo.ProcessConfig;

namespace Echo.Tests
{
    public class IssuesTests
    {
        [Collection("Initialise")]
        public class Issue31
        {
            public class AskFixture : IDisposable
            {
                public AskFixture()
                {
                    initialiseFileSystem();
                    var command = Router.leastBusy
                        (
                            Name: "cmd",
                            Count: 5,
                            Inbox: (string msg) =>
                            {
                                throw new Exception("Hello");
                            }

                        );
                    register(command.Name, command);
                    subscribe<Exception>(Errors(), e => raise<Unit>(e));
                }
                public void Dispose() => shutdownAll();
            }


            public class AskError : IClassFixture<AskFixture>, IDisposable
            {
                //public readonly ITestOutputHelper Output;
                public readonly IReadOnlyCollection<IDisposable> Trash;

                public AskError()
                {
                    Trash = List(
                        subscribe<Exception>(Errors(), e =>
                            Console.WriteLine($"{e.GetType().ToString()} {e.Message}"))
                    );
                }

                public void Dispose()
                {
                    Trash.Iter(t => t.Dispose());
                }

                [Fact]
                public void DoesntRaiseExceptionOnAskWithNoInbox()
                {
                    Assert.Throws<Echo.ProcessException>(() => ask<string>("@test", "You there?"));
                }

                [Fact]
                public void RaisesExceptionsOnAskWithInboxException()
                {
                    Assert.Throws<Echo.ProcessException>(() => ask<string>("@cmd", "You there?"));
                }
            }
        }
    }

    public class Issue69ForKill
    {
        [Fact(Timeout = 5000)]
        public void TryKillChildren()
        {
            initialiseFileSystem();

            var parent = spawn<string>("parent", ParentInbox);
            for (int i = 0; i < 10; i++)
            {
                tell(parent, "tell");
                tell(parent, "kill");
            }

            // call ask to prove the system is not in a deadlock
            var res = ask<int>(parent, "ask");

            Assert.True(res == 1);
        }

        static void ParentInbox(string cmd) =>
            ignore(cmd == "tell" ? fwd(SpawnChild())
                 : cmd == "kill" ? kill(Children["child"])
                 : cmd == "ask"  ? reply(1)
                 : unit);

        static ProcessId SpawnChild() =>
            spawn<int, string>(
                "child",
                () => throw new Exception(),
                (s, _) => s);
    }

    public class Issue69ForShutdown
    {
        [Fact(Timeout = 5000)]
        public void TryShutdownAll()
        {
            for (int i = 0; i < 100; i++)
            {
                initialiseFileSystem();
                var parent = spawn<int>("parent", ParentInbox);
                for (int j = 0; j < 100; j++)
                {
                    tell(parent, j);
                }
                shutdownAll();
            }

            // call ask to prove the system is not in a deadlock
            initialiseFileSystem();
            var pid = spawn<int>("parent", ParentInbox);
            var res = ask<int>(pid, -1);

            Assert.True(res == 1);
        }

        static void ParentInbox(int id) =>
            ignore(id >= 0
                        ? fwd(SpawnChild(id))
                        : reply(1));

        static ProcessId SpawnChild(int id) =>
            spawn<IDisposable, string>(
                $"child-{id}", 
                () => throw new Exception(), 
                (s, _) => s);
    }
}
