using System.ClientModel;
using System.Text;
using System.Text.Json;

using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Mvc;
using OpenAI;
using Rystem.OpenAi;

using Aikido.Zen.Core.Models;

using AmazonContentBlock = Amazon.BedrockRuntime.Model.ContentBlock;
using AmazonMessage = Amazon.BedrockRuntime.Model.Message;

namespace DotNetCore.Sample.App.Controllers
{

    [ApiController]
    [Route("llm-usage")]
    public class LlmUsageController : ControllerBase
    {

        private OpenAIClient _openaiClient;
        private AzureOpenAIClient _azureOpenAiClient;
        private IOpenAi _rystemOpenAiClient;
        private AmazonBedrockRuntimeClient _awsBedrockClient;
        private readonly IConfiguration _configuration;
        private readonly IDictionary<string, string> _apiKeys = new Dictionary<string, string>();

        public LlmUsageController(IConfiguration configuration)
        {
            _configuration = configuration;
            _apiKeys["OpenAI"] = configuration["AI:OpenAIApiKey"];
            _apiKeys["Anthropic"] = configuration["AI:AnthropicApiKey"];
            _apiKeys["Gemini"] = configuration["AI:GeminiApiKey"];
            _apiKeys["BedrockAccessKey"] = configuration["AI:Bedrock:AccessKey"];
            _apiKeys["BedrockSecretKey"] = configuration["AI:Bedrock:SecretKey"];
            _apiKeys["AzureOpenAI"] = configuration["AI:AzureOpenAIApiKey"];
        }

        [HttpGet("request/provider/{provider}/model/{model}")]
        public async Task<IActionResult> GetUsage([FromRoute] string provider, [FromRoute] string model, [FromQuery] string input, [FromQuery] string method="")
        {
            switch (provider)
            {
                case "openai":
                    return Ok(OpenaiRequest(provider, model, input));
                case "azure":
                    return Ok(AzureOpenaiRequest(provider, model, input));
                case "rystem":
                    return Ok(RystemOpenAiRequest(provider, model, input));
                case "bedrock":
                    {
                        if (method == "Converse")
                            return Ok(await BedrockConverseRequest(provider, model, input));
                        if (method == "ConverseStream")
                            return Ok(await BedrockConverseStreamRequest(provider, model, input));
                        if (method == "InvokeModel")
                            return Ok(await BedrockInvokeModelRequest(provider, model, input));

                        return Ok(await BedrockConverseRequest(provider, model, input));
                    }
                default:
                    return BadRequest("Invalid provider");
            }
        }

        private async Task<string> BedrockConverseRequest(string provider, string model, string input)
        {
            _awsBedrockClient = new AmazonBedrockRuntimeClient(new BasicAWSCredentials(_apiKeys["BedrockAccessKey"], _apiKeys["BedrockSecretKey"]), RegionEndpoint.EUCentral1);
            var request = new ConverseRequest()
            {
                ModelId = model,
                Messages =
                [
                    new AmazonMessage()
                    {
                        Role = "user",
                        Content =
                        [
                            new AmazonContentBlock { Text = input }
                        ]
                    }
                ]
            };
            try
            {
                var response = await _awsBedrockClient.ConverseAsync(request);

                var sb = new StringBuilder();
                var contentBlocks = response?.Output?.Message?.Content;

                if (contentBlocks is not null)                
                    foreach (var block in contentBlocks)                    
                        if (!string.IsNullOrEmpty(block.Text))
                            sb.Append(block.Text);

                return sb.ToString();
            }
            catch (AmazonBedrockRuntimeException e)
            {
                Console.WriteLine($"ERROR: Can't invoke '{model}'. Reason: {e.Message}");
                throw;
            }
        }

        private async Task<string> BedrockInvokeModelRequest(string provider, string model, string input)
        {
            _awsBedrockClient = new AmazonBedrockRuntimeClient(new BasicAWSCredentials(_apiKeys["BedrockAccessKey"], _apiKeys["BedrockSecretKey"]), RegionEndpoint.EUCentral1);
            
            try
            {
                var modelId = model;
                // Model-native JSON payload
                var payload = new { anthropic_version = "bedrock-2023-05-31", max_tokens = 512, temperature = 0.5, messages = new[] { new { role = "user", content = input } } };
                var json = JsonSerializer.Serialize(payload);
                var request = new InvokeModelRequest { ModelId = modelId, ContentType = "application/json", Accept = "application/json", Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)) };

                var response = await _awsBedrockClient.InvokeModelAsync(request);
                // Read response JSON
                response.Body.Position = 0;
                using var reader = new StreamReader(response.Body);
                var responseJson = await reader.ReadToEndAsync();
                return responseJson;
            }
            catch (AmazonBedrockRuntimeException e)
            {
                Console.WriteLine($"ERROR: Can't invoke '{model}'. Reason: {e.Message}");
                throw;
            }
        }

        private async Task<string> BedrockConverseStreamRequest(string provider, string model, string input)
        {
            _awsBedrockClient = new AmazonBedrockRuntimeClient(new BasicAWSCredentials(_apiKeys["BedrockAccessKey"], _apiKeys["BedrockSecretKey"]), RegionEndpoint.USEast1);

            var request = new ConverseStreamRequest()
            {
                ModelId = model,
                Messages =
                [
                    new()
                    {
                        Role = "user",
                        Content =
                        [
                            new() { Text = input }
                        ]
                    }
                ]
            };

            try
            {
                var response = await _awsBedrockClient.ConverseStreamAsync(request);

                var sb = new StringBuilder();
                using var output = response.Stream;

                // Append every streamed text delta
                output.ContentBlockDeltaReceived += (s, e) =>
                {
                    var text = e.EventStreamEvent?.Delta?.Text;
                    if (!string.IsNullOrEmpty(text))
                        sb.Append(text);
                };

                // Drive the stream so the events above fire
                await foreach (var _ in output) { /* no-op */ }

                return sb.ToString();
            }
            catch (AmazonBedrockRuntimeException e)
            {
                Console.WriteLine($"ERROR: Can't invoke '{model}'. Reason: {e.Message}");
                throw;
            }
        }

        private string OpenaiRequest(string provider, string model, string input)
        {
            _openaiClient = CreateOpenAiClient(_configuration, provider);
            var client = _openaiClient.GetChatClient(model);
            var response = client.CompleteChat(input);
            return response.Value.Content[0].Text;
        }

        private string AzureOpenaiRequest(string provider, string model, string input)
        {
            _azureOpenAiClient = CreateAzureOpenAiClient(_configuration, provider);
            var client = _azureOpenAiClient.GetChatClient(model);
            var response = client.CompleteChat(input);
            return response.Value.Content[0].Text;
        }

        private string RystemOpenAiRequest(string provider, string model, string input)
        {
            _rystemOpenAiClient = CreateRystemOpenAiClient(_configuration, provider);
            var response = _rystemOpenAiClient.Chat
            .WithModel(model)
            .AddMessage(input)
            .ExecuteAsync().Result;
            return response?.Choices?[0]?.Message!.Content! ?? string.Empty;
        }

        private OpenAIClient CreateOpenAiClient(IConfiguration configuration, string provider)
        {
            var endpoint = configuration["AI:OpenAIEndpoint"]!;
            var token = _apiKeys["OpenAI"];
            switch (provider)
            {
                case "openai":
                    endpoint = configuration["AI:OpenAIEndpoint"]!;
                    token = _apiKeys["OpenAI"];
                    break;
                case "azure":
                    endpoint = configuration["AI:AzureOpenAIEndpoint"]!;
                    token = _apiKeys["AzureOpenAI"];
                    break;
                case "anthropic":
                    endpoint = configuration["AI:AnthropicEndpoint"]!;
                    token = _apiKeys["Anthropic"];
                    break;
                case "gemini":
                    endpoint = configuration["AI:GeminiEndpoint"]!;
                    token = _apiKeys["Gemini"];
                    break;
            }
            var client = new OpenAIClient(new ApiKeyCredential(token), new OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint),
            });
            return client;
        }

        private AzureOpenAIClient CreateAzureOpenAiClient(IConfiguration configuration, string provider)
        {
            var endpoint = configuration["AI:OpenAIEndpoint"]!;
            var token = _apiKeys["OpenAI"];
            switch (provider)
            {
                case "openai":
                    endpoint = configuration["AI:OpenAIEndpoint"]!;
                    token = _apiKeys["OpenAI"];
                    break;
                case "azure":
                    endpoint = configuration["AI:AzureOpenAIEndpoint"]!;
                    token = _apiKeys["AzureOpenAI"];
                    break;
                case "anthropic":
                    endpoint = configuration["AI:AnthropicEndpoint"]!;
                    token = _apiKeys["Anthropic"];
                    break;
                case "gemini":
                    endpoint = configuration["AI:GeminiEndpoint"]!;
                    token = _apiKeys["Gemini"];
                    break;
            }
            var client = new AzureOpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(token));
            return client;
        }

        private Rystem.OpenAi.IOpenAi CreateRystemOpenAiClient(IConfiguration configuration, string provider)
        {
            var token = _apiKeys["OpenAI"];
            switch (provider)
            {
                case "rystem":
                case "openai":
                    token = _apiKeys["OpenAI"];
                    break;
                default:
                    token = null;
                    break;
            }
            var azureResourceName = configuration["AI:AzureOpenAIResourceName"]!;
            var azureManagedIdentityClientId = configuration["AI:AzureOpenAIManagedIdentityClientId"]!;
            var azureClientId = configuration["AI:AzureOpenAIClientId"]!;
            var azureTenantId = configuration["AI:AzureOpenAITenantId"]!;
            var azureClientSecret = configuration["AI:AzureOpenAIClientSecret"]!;
            OpenAiServiceLocator.Configuration.AddOpenAi(settings =>
            {
                if (!string.IsNullOrEmpty(token))
                    settings.ApiKey = token;
                if (!string.IsNullOrEmpty(azureResourceName))
                    settings.Azure.ResourceName = azureResourceName;
                if (!string.IsNullOrEmpty(azureManagedIdentityClientId))
                    settings.Azure.ManagedIdentity.Id = azureManagedIdentityClientId;
                if (!string.IsNullOrEmpty(azureClientId))
                    settings.Azure.AppRegistration.ClientId = azureClientId;
                if (!string.IsNullOrEmpty(azureTenantId))
                    settings.Azure.AppRegistration.TenantId = azureTenantId;
                if (!string.IsNullOrEmpty(azureClientSecret))
                    settings.Azure.AppRegistration.ClientSecret = azureClientSecret;
            }, "client");
            return OpenAiServiceLocator.Instance.Create("client");
        }
    }
}
