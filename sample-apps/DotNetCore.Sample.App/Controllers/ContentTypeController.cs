using Aikido.Zen.Core;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Xml;

namespace DotNetCore.Sample.App.Controllers
{
    [ApiController]
    [Route("contenttype")]
    public class ContentTypeController : ControllerBase
    {
        [HttpPost("form"), Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> HandleFormData()
        {
            var form = await Request.ReadFormAsync();
            var data = form.ToDictionary(x => x.Key, x => x.Value.ToString());
            var context = (Context)HttpContext.Items["Aikido.Zen.Context"];
            return Ok(context.ParsedUserInput);
        }

        [HttpPost("multipart"), Consumes("multipart/form-data")]
        public async Task<IActionResult> HandleMultipartForm()
        {
            var context = (Context)HttpContext.Items["Aikido.Zen.Context"];
            return Ok(context.ParsedUserInput);
        }

        [HttpPost("text"), Consumes("text/plain")]
        public async Task<IActionResult> HandleTextData()
        {
            using var reader = new StreamReader(Request.Body);
            var content = await reader.ReadToEndAsync();
            return Ok(new { Text = content });
        }

        [HttpPost("json"), Consumes("application/json")]
        public async Task<IActionResult> HandleJsonData()
        {
            var context = (Context)HttpContext.Items["Aikido.Zen.Context"];
            return Ok(context.ParsedUserInput);
        }

        [HttpPost("xml"), Consumes("application/xml")]
        public IActionResult HandleXmlData()
        {
            var context = (Context)HttpContext.Items["Aikido.Zen.Context"];
            return Ok(context.ParsedUserInput);
        }
    }
}
