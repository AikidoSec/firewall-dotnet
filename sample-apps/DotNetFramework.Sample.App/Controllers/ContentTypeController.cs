using System.Net.Http.Formatting;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace DotNetFramework.Sample.App.Controllers
{
    /// <summary>
    /// Controller for handling different content type requests
    /// </summary>
    [RoutePrefix("api/contenttype")]
    public class ContentTypeController : ApiController
    {
        /// <summary>
        /// Handles form URL encoded request
        /// </summary>
        /// <returns>The received form data</returns>
        [HttpPost]
        [Route("form")]
        public IHttpActionResult PostFormUrlEncoded([FromBody] FormDataCollection formData)
        {
            return Ok(HttpContext.Current.Items["Aikido.Zen.Context"]);
        }

        /// <summary>
        /// Handles multipart form data request
        /// </summary>
        /// <returns>Information about the received files and form data</returns>
        [HttpPost]
        [Route("multipart")]
        public IHttpActionResult PostMultipartFormData()
        {
            return Ok(HttpContext.Current.Items["Aikido.Zen.Context"]);
        }

        /// <summary>
        /// Handles plain text request
        /// </summary>
        /// <returns>The received text</returns>
        [HttpPost]
        [Route("text")]
        public IHttpActionResult PostText()
        {
            return Ok(HttpContext.Current.Items["Aikido.Zen.Context"]);
        }

        /// <summary>
        /// Handles JSON request
        /// </summary>
        /// <returns>The received JSON data</returns>
        [HttpPost]
        [Route("json")]
        public IHttpActionResult PostJson()
        {
            return Ok(HttpContext.Current.Items["Aikido.Zen.Context"]);
        }

        /// <summary>
        /// Handles XML request
        /// </summary>
        /// <returns>The received XML data</returns>
        [HttpPost]
        [Route("xml")]
        public IHttpActionResult PostXml()
        {
            // we consume xml, but want to return json
            HttpContext.Current.Response.ContentType = "application/json";
            return Json(HttpContext.Current.Items["Aikido.Zen.Context"]);
        }
    }
}
