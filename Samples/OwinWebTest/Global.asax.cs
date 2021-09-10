using System;
using System.Web;
using LanguageExt;
using static LanguageExt.Prelude;
using Echo;
using static Echo.Process;
using System.IO;
using Echo.ProcessJS;

namespace OwinWebTest
{
    public class Global : HttpApplication
    {
        static readonly Func<Unit> Startup = memo(() =>
        {
            // Register redis persistence provider
            RedisCluster.register();

            // Load the config for this web-site 
            ProcessConfig.initialiseWeb("localhost");

            // Allow messages to all processes from the browser
            ProcessHub.RouteValidator = _ => true;

            // Tool logging service for diagnostics
            ProcessLog.startup(None);

            // Spawn a ticking clock
            var clock = spawn<Unit>("clock", _ =>
            {
                publish(DateTime.Now.ToString());
                tellSelf(_, 1 * second);
            });

            // Reply to anything received
            spawn<string>("echo", msg =>
            {
                ProcessLog.tellInfo($"echoing {msg}");
                replyOrTellSender(new { tag = "rcv", value = msg });
            });

            // Send hello to anything received
            spawn<string>("hello", msg => reply("Hello, " + msg));

            // Start the clock ticking
            tell(clock, unit);

            return unit;
        });

        protected void Application_Start(object sender, EventArgs e)
        {

        }

        protected void Session_Start(object sender, EventArgs e)
        {

        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            Startup();
        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {

        }

        protected void Application_Error(object sender, EventArgs e)
        {

        }

        protected void Session_End(object sender, EventArgs e)
        {

        }

        protected void Application_End(object sender, EventArgs e)
        {

        }
    }
}