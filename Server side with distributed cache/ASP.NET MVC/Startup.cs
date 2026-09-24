
using Microsoft.Owin;
using Owin;
using Syncfusion.Collaboration.Core.Extensions;
using System.Threading.Tasks;
using System.Web;

[assembly: OwinStartup(typeof(WebApplication1.Startup))]

namespace WebApplication1
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.Map("/ws", map =>
            {
                map.Run(context =>
                {
                    var httpContext = HttpContext.Current;

                    if (httpContext == null)
                    {
                        context.Response.StatusCode = 500;
                        return Task.CompletedTask;
                    }

                    if (!httpContext.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = 400;
                        return context.Response.WriteAsync(
                            "WebSocket connection required.");
                    }                    
                    httpContext.AcceptWebSocketRequest(ServiceCollectionExtensions.MapCollaborationServer());
                    return Task.CompletedTask;
                });
            });
        }
       
    }
}
