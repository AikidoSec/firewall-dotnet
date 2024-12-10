using System;
using System.Web.Mvc;

namespace DotNetFramework.Sample.App.Controllers
{
    [RoutePrefix("benchmark")]
    public class BenchmarkController : Controller
    {
        private const string html = "<html><head><title>Test</title></head><body><h1>Test</h1></body></html>";
        
        [Route("withfirewall")]
        public ContentResult WithFirewall()
        {
            // return html
            Environment.SetEnvironmentVariable("AIKIDO_DISABLE", null);
            return Content(html, "text/html");
        }

        [Route("withoutfirewall")]
        public System.Web.Mvc.ContentResult WithoutFirewall()
        {
            // return html
            Environment.SetEnvironmentVariable("AIKIDO_DISABLE", "true");
            return Content(html, "text/html");
        }
    }
}
