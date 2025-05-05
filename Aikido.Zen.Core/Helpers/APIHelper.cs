using System;
using System.Net.Http;
using System.Text.Json;
using Aikido.Zen.Core.Api;

namespace Aikido.Zen.Core.Helpers
{
    public class APIHelper
    {
        public static HttpRequestMessage CreateRequest(string token, Uri baseUrl, string path, HttpMethod method, HttpContent content = null)
        {
            var request = new HttpRequestMessage(method, new Uri(baseUrl, path));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(token);
            request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));

            if (content != null)
            {
                request.Content = content;
            }

            return request;
        }

        public static T ToAPIResponse<T>(HttpResponseMessage response) where T : APIResponse, new()
        {
            if ((int)response.StatusCode == 429) // Too many requests
            {
                return new T { Success = false, Error = "rate_limited" };
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new T { Success = false, Error = "invalid_token" };
            }

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                try
                {
                    var data = response.Content.ReadAsStringAsync().Result;
                    var result = JsonSerializer.Deserialize<T>(data, ZenApi.JsonSerializerOptions);
                    result.Success = true;
                    return result;
                }
                catch
                {
                    // Fall through
                }
            }

            return new T { Success = false, Error = "unknown_error" };
        }
    }
}
