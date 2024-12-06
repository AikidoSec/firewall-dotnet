using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using RestSharp;

namespace DotNetFramework.Sample.App.Controllers
{
    /// <summary>
    /// Controller for handling outbound HTTP requests
    /// </summary>
    [RoutePrefix("api/outbound")]
    public class OutboundController : ApiController
    {
        /// <summary>
        /// Demonstrates outbound HTTP requests using HttpClient
        /// </summary>
        /// <param name="domainName">The domain name to send the request to</param>
        /// <returns>The HTTP response from the request</returns>
        [Route("httpclient/{domainName}")]
        public async Task<HttpResponseMessage> GetHttpClient(string domainName)
        {
            domainName = Uri.UnescapeDataString(domainName);
            var client = new HttpClient();
            client.BaseAddress = new Uri(domainName);
            return await client.GetAsync("", HttpCompletionOption.ResponseHeadersRead);
        }

        /// <summary>
        /// Demonstrates outbound HTTP requests using WebRequest
        /// </summary>
        /// <param name="domainName">The domain name to send the request to</param>
        /// <returns>The HTTP response from the request</returns>
        [Route("webrequest/{domainName}")]
        public async Task<WebResponse> GetWebRequest(string domainName)
        {
            domainName = Uri.UnescapeDataString(domainName);
            var request = HttpWebRequest.Create(domainName);
            // only HEAD
            request.Method = "HEAD";
            return await request.GetResponseAsync();
        }

        /// <summary>
        /// Demonstrates outbound HTTP requests using RestSharp
        /// </summary>
        /// <param name="domainName">The domain name to send the request to</param>
        /// <returns>The HTTP response from the request</returns>
        [Route("restsharp/{domainName}")]
        public async Task<RestResponse> GetRestSharp(string domainName)
        {
            domainName = Uri.UnescapeDataString(domainName);
            var client = new RestClient(domainName);
            var request = new RestRequest();
            request.CompletionOption = HttpCompletionOption.ResponseHeadersRead;
            return await client.ExecuteAsync(request);
        }
    }
}
