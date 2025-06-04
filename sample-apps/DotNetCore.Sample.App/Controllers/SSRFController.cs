using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCore.Sample.App.Controllers
{
    /// <summary>
    /// Controller for fetching external resources, intended to simulate scenarios
    /// relevant to Server-Side Request Forgery (SSRF) testing.
    /// It provides an endpoint to request content from a user-supplied URL.
    /// </summary>
    [ApiController]
    [Route("images")]
    public class SSRFController : ControllerBase
    {
        // HttpClient is intended to be instantiated once and re-used throughout the life of an application.
        // Instantiating an HttpClient class for every request will exhaust the number of sockets available under heavy loads.
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// Fetches content from the specified URL.
        /// The URL is provided as a catch-all route parameter. ASP.NET Core will URL-decode the value.
        /// </summary>
        /// <param name="imageUrl">The URL of the resource to fetch. This is captured from the route path and should be a fully qualified URL.</param>
        /// <returns>
        /// An <see cref="IActionResult"/> that results in:
        /// - 200 OK with a success JSON object if the external resource is retrieved successfully.
        /// - 400 Bad Request if the imageUrl is null or empty.
        /// - 500 Internal Server Error if the request to the external resource fails (e.g., network error, non-success HTTP status from the target, or other exceptions).
        /// This 500 error mimics how a security-blocked request might manifest.
        /// </returns>
        [HttpGet("{*imageUrl}")]
        public async Task<IActionResult> GetImageAsync([FromRoute] string imageUrl)
        {
            imageUrl = HttpUtility.UrlDecode(imageUrl);
            if (string.IsNullOrEmpty(imageUrl))
            {
                return BadRequest(new { success = false, message = "Image URL cannot be empty." });
            }

            // Ensure the URL is a valid absolute URI
            if (!System.Uri.TryCreate(imageUrl, System.UriKind.Absolute, out System.Uri requestUri))
            {
                return BadRequest(new { success = false, message = "Invalid URL format provided." });
            }

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(requestUri);

                if (response.IsSuccessStatusCode)
                {
                    return Ok(new { success = true, requestedUrl = imageUrl });
                }
                else
                {
                    return StatusCode(500, new { success = false, requestedUrl = imageUrl, error = $"Failed to fetch resource. Status code: {response.StatusCode}" });
                }
            }
            catch (HttpRequestException ex)
            {
                // This exception can occur for various reasons, including DNS resolution failures,
                // connection refusals, or other network-level issues.
                // It can also be a manifestation of a security tool blocking the outbound request.
                return StatusCode(500, new { success = false, requestedUrl = imageUrl, error = ex.Message });
            }
            catch (System.Exception ex) // Catch-all for other unexpected errors
            {
                return StatusCode(500, new { success = false, requestedUrl = imageUrl, error = $"An unexpected error occurred: {ex.Message}" });
            }
        }
    }
}
