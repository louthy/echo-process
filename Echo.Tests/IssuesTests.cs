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
}
