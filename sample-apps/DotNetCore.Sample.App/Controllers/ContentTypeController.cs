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
            return Ok(HttpContext.Items["Aikido.Zen.Context"]);
        }

        [HttpPost("multipart"), Consumes("multipart/form-data")]
        public async Task<IActionResult> HandleMultipartForm()
        {
            return Ok(HttpContext.Items["Aikido.Zen.Context"]);
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
            var json = await new StreamReader(Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<IDictionary<string, object>>(json);
            return Ok(HttpContext.Items["Aikizo.Zen.Context"]);
        }

        [HttpPost("xml"), Consumes("application/xml")]
        public IActionResult HandleXmlData()
        {
            var xml = new StreamReader(Request.Body).ReadToEndAsync().Result;
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return Ok(HttpContext.Items["Aikido.Zen.Context"]);
        }
    }
} 
