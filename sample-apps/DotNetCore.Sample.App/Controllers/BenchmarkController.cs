using Microsoft.AspNetCore.Mvc;

namespace DotNetCore.Sample.App.Controllers
{
    [ApiController]
    [Route("benchmark")]
    public class BenchmarkController : ControllerBase
    {

        private const string html = "<html><head><title>Test</title></head><body><h1>Test</h1></body></html>";

        [HttpGet("hello")]
        public IActionResult Benchmark1()
        {
            return Ok(html);
        }
    }
}
