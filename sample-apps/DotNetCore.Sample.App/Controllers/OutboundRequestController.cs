using Microsoft.AspNetCore.Mvc;
using RestSharp;
using System.Net;

namespace DotNetCore.Sample.App.Controllers
{
    [ApiController]
    [Route("outbound")]
    public class OutboundRequestController : ControllerBase
    {
        [HttpGet("httpclient/{domainName}")]
        public async Task<IActionResult> DoHttpClientRequest(string domainName)
        {
            domainName = Uri.UnescapeDataString(domainName);
            var client = new HttpClient();
            client.BaseAddress = new Uri(domainName);
            var response = await client.GetAsync("", HttpCompletionOption.ResponseHeadersRead);
            return Ok(response);
        }

        [HttpGet("webrequest/{domainName}")]
        public async Task<IActionResult> DoWebRequest(string domainName)
        {
            domainName = Uri.UnescapeDataString(domainName);
            var request = WebRequest.Create(domainName);
            request.Method = "HEAD";
            var response = await request.GetResponseAsync();
            return Ok(response);
        }

        [HttpGet("restsharp/{domainName}")]
        public async Task<IActionResult> RestSharpRequest(string domainName)
        {
            domainName = Uri.UnescapeDataString(domainName);
            var client = new RestClient(domainName);
            var request = new RestRequest();
            request.CompletionOption = HttpCompletionOption.ResponseHeadersRead;
            var response = await client.ExecuteAsync(request);
            return Ok(response);
        }
    }
} 
