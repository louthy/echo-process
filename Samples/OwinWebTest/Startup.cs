using Owin;
using LanguageExt;
using static LanguageExt.Prelude;
using Echo;
using static Echo.Process;
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
            ProcessOwin.initialise(app);
        }
    }
}