using Owin;
using LanguageExt;
using static LanguageExt.Prelude;
using static LanguageExt.Process;
using Microsoft.Owin;
using System;
using System.Threading;

[assembly: OwinStartup(typeof(OwinWebTest.Startup))]
namespace OwinWebTest
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ProcessConfig.initialiseWeb("root");
            ProcessOwin.initialise(app);
            ProcessHub.RouteValidator = _ => true;

            var clock = spawn<Unit>("clock", _ =>
            {
                publish(DateTime.Now.ToString());
                tellSelf(_, 1 * second);
            });

            Thread.Sleep(5000);

            spawn<string>("echo", msg => replyOrTellSender(new { tag = "rcv", value = msg }));

            spawn<string>("hello", msg => reply("Hello, " + msg));

            tell(clock, unit);
        }
    }
}