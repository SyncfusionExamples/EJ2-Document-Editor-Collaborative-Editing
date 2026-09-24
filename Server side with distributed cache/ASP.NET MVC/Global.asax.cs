using Syncfusion.Collaboration.Core.Extensions;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using WebApplication1.Adapter;
namespace WebApplication1
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);           
            ServiceCollectionExtensions.RegisterAdapter(
                new DocumentEditorCollaborationAdapter());
            ServiceCollectionExtensions.AddCollaborationServer(options =>{
            options.ConnectionString = "Your Redis Connection string";
            options.ConnectionType = CollaborationConnectionType.WebSocket;            
            });
           
        }
    }
}
