using System.ClientModel;
using AWSSDK;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Mvc;
using OpenAI;
using OpenAI.Chat;
using Rystem.OpenAi;

namespace DotNetCore.Sample.App.Controllers
{

    [ApiController]
    [Route("llm-usage")]
    public class LlmUsageController : ControllerBase
    {

        private OpenAIClient _openaiClient;
        private AzureOpenAIClient _azureOpenAiClient;
        private Rystem.OpenAi.IOpenAi _rystemOpenAiClient;
        private Amazon.Bedrock.AmazonBedrockClient _awsBedrockClient;
        private readonly IConfiguration _configuration;
        private readonly IDictionary<string, string> _apiKeys = new Dictionary<string, string>();

        public LlmUsageController(IConfiguration configuration)
        {
            _configuration = configuration;
            _apiKeys["OpenAI"] = configuration["AI:OpenAIApiKey"];
            _apiKeys["Anthropic"] = configuration["AI:AnthropicApiKey"];
            _apiKeys["Gemini"] = configuration["AI:GeminiApiKey"];
            _apiKeys["Bedrock"] = configuration["AI:BedrockApiKey"];
            _apiKeys["AzureOpenAI"] = configuration["AI:AzureOpenAIApiKey"];
        }

        [HttpGet("request/provider/{provider}/model/{model}")]
        public IActionResult GetUsage([FromRoute] string provider, [FromRoute] string model, [FromQuery] string input)
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
                    throw new NotImplementedException("Bedrock is not implemented");
                default:
                    return BadRequest("Invalid provider");
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
                settings.ApiKey = token;
                settings.Azure.ResourceName = azureResourceName;
                settings.Azure.ManagedIdentity.Id = azureManagedIdentityClientId;
                settings.Azure.AppRegistration.ClientId = azureClientId;
                settings.Azure.AppRegistration.TenantId = azureTenantId;
                settings.Azure.AppRegistration.ClientSecret = azureClientSecret;
            }, "client");
            return OpenAiServiceLocator.Instance.Create("client");
        }
    }
}
