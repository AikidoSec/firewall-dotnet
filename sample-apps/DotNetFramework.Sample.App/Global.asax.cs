using Aikido.Zen.DotNetFramework;
using System;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace DotNetFramework.Sample.App
{
	public class WebApiApplication : System.Web.HttpApplication
	{
		protected void Application_Start()
		{
            AreaRegistration.RegisterAllAreas();
			GlobalConfiguration.Configure(WebApiConfig.Register);
			FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
			RouteConfig.RegisterRoutes(RouteTable.Routes);
			BundleConfig.RegisterBundles(BundleTable.Bundles);
            Environment.SetEnvironmentVariable("AIKIDO_DEBUG", "true");
            Zen.Start();
		}
	}
}
