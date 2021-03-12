using System;
using Microsoft.AspNetCore.Builder;

namespace Echo
{
    public static class ProcessSysMiddlewareExtensions
    {
        public static IApplicationBuilder UseProcessSys(this IApplicationBuilder builder) =>        
            builder.UseWhen(
                predicate: context => context.Request.Path == "/process-sys",
                configuration: appBuilder =>
                               {
                                   appBuilder.UseWebSockets();
                                   appBuilder.Use(async (context, next) =>
                                                  {
                                                      if (context.WebSockets.IsWebSocketRequest)
                                                      {
                                                          await new ProcessSysListener(context).Listen();
                                                      }
                                                  });
                               });
    }    
}