using Microsoft.AspNetCore.Mvc;

namespace DotNetCore.Sample.App.Controllers
{
    [ApiController]
    [Route("benchmark")]
    public class BenchmarkController : ControllerBase
    {

        private const string html = "<html><head><title>Test</title></head><body><h1>Test</h1></body></html>";

        [HttpGet("with-firewall")]
        public IActionResult Benchmark1()
        {
            Environment.SetEnvironmentVariable("AIKIDO_DISABLE", null);
            return Ok(html);
        }

        [HttpGet("without-firewall")]
        public IActionResult Benchmark2()
        {
            Environment.SetEnvironmentVariable("AIKIDO_DISABLE", "true");
            return Ok(html);
        }
    }
}
