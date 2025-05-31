using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCore.Sample.App.Controllers
{
    /// <summary>
    /// Controller demonstrating potential shell injection vulnerabilities.
    /// Executes commands provided via query parameters.
    /// </summary>
    [ApiController]
    [Route("ssrf")]
    public class SSRFController : ControllerBase
    {
        [HttpGet("direct")]
        public async Task<IActionResult> Direct([FromQuery]string url = "https://localhost:8080/evil-request")
        {
            new HttpClient().GetAsync(url).Wait();
            return new JsonResult(new { success = true });
        }

        [HttpGet("redirect")]
        public async Task<IActionResult> UsingRedirect([FromQuery] string url = "https://httpbin.org/redirect-to")
        {
            new HttpClient().GetAsync(url + "?url=https://localhost:8080/evil-request").Wait();
            return new JsonResult(new { success = true });
        }
    }
}
