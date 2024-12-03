using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aikido.Zen.Core.Api
{
	public class ReportingAPIClient : IReportingAPIClient
	{
		private readonly HttpClient _httpClient;
		private readonly Uri _reportingUrl;

		public ReportingAPIClient(Uri reportingUrl)
		{
			_httpClient = new HttpClient();
			_reportingUrl = reportingUrl;
		}

		private ReportingAPIResponse ToAPIResponse(HttpResponseMessage response)
		{
			if ((int)response.StatusCode == 429) // Too many requests
			{
				return new ReportingAPIResponse { Success = false, Error = "rate_limited" };
			}

			if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
			{
				return new ReportingAPIResponse { Success = false, Error = "invalid_token" };
			}

			if (response.StatusCode == System.Net.HttpStatusCode.OK)
			{
				try
				{
					var data = response.Content.ReadAsStringAsync().Result;
					return JsonSerializer.Deserialize<ReportingAPIResponse>(data);
				}
				catch
				{
					// Fall through
				}
			}

			return new ReportingAPIResponse { Success = false, Error = "unknown_error" };
		}

		public async Task<ReportingAPIResponse> ReportAsync(string token, Event @event, int timeoutInMS)
		{
			using (var cts = new CancellationTokenSource(timeoutInMS))
			{

				var requestContent = new StringContent(JsonSerializer.Serialize(@event), Encoding.UTF8, "application/json");
				var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_reportingUrl, "api/runtime/events"))
				{
					Content = requestContent
				};
				request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

				try
				{
					var response = await _httpClient.SendAsync(request, cts.Token);
					return ToAPIResponse(response);
				}
				catch (TaskCanceledException)
				{
					if (!cts.Token.IsCancellationRequested)
						return new ReportingAPIResponse { Success = false, Error = "timeout" };

					throw;
				}
				catch (Exception ex)
				{
					throw new Exception("An error occurred while reporting", ex);
				}
			}
		}
	}

	public class Event
	{
		// Add appropriate event properties
	}

	public class ReportingAPIResponse
	{
		public bool Success { get; set; }
		public string Error { get; set; }
	}
}
