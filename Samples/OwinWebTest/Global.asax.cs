using System;
using System.Web;
using LanguageExt;
using static LanguageExt.Prelude;
using Echo;
using System.IO;

namespace OwinWebTest
{
    public class Global : HttpApplication
    {
        static readonly Func<Unit> Startup = memo(() =>
        {
            var hostName = HttpContext.Current.Request.Url.Host;

            File.WriteAllText("c:\\temp\\output.txt", hostName);

            ProcessConfig.initialiseWeb(hostName);
            ProcessHub.RouteValidator = _ => true;
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