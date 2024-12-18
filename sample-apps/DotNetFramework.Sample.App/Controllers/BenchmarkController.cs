using System;
using System.Web.Mvc;

namespace DotNetFramework.Sample.App.Controllers
{
    [RoutePrefix("benchmark")]
    public class BenchmarkController : Controller
    {
        private const string html = "<html><head><title>Test</title></head><body><h1>Test</h1></body></html>";
        
        [Route("hello")]
        public ContentResult WithFirewall()
        {
            // return html
            return Content(html, "text/html");
        }
    }
}
