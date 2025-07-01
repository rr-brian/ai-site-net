using Microsoft.AspNetCore.Mvc;
using Azure;
using Azure.AI.OpenAI;
using System.Text.Json.Serialization;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IConfiguration configuration, ILogger<ChatController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public class ChatRequest
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    public class ChatResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = "";
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> PostAsync([FromBody] ChatRequest request)
    {
        try
        {
            // Get configuration values
            var apiKey = _configuration["OpenAI:ApiKey"] ?? 
                Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var endpoint = _configuration["OpenAI:Endpoint"] ?? 
                Environment.GetEnvironmentVariable("OPENAI_ENDPOINT") ?? 
                "https://generalsearchai.openai.azure.com/";
            var deploymentName = _configuration["OpenAI:DeploymentName"] ?? 
                Environment.GetEnvironmentVariable("OPENAI_DEPLOYMENT_NAME") ?? 
                "gpt-4.1";
            var apiVersion = _configuration["OpenAI:ApiVersion"] ?? 
                Environment.GetEnvironmentVariable("OPENAI_API_VERSION") ?? 
                "2025-01-01-preview";

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("OpenAI API key not found");
                return BadRequest("OpenAI API key not configured");
            }

            if (string.IsNullOrEmpty(endpoint))
            {
                _logger.LogError("OpenAI endpoint not found");
                return BadRequest("OpenAI endpoint not configured");
            }

            // Create Azure Key credential
            AzureKeyCredential credential = new AzureKeyCredential(apiKey);

            // Initialize the OpenAIClient with Azure options
            var clientOptions = new OpenAIClientOptions();
            
            var client = new OpenAIClient(
                new Uri(endpoint),
                credential,
                clientOptions);
            
            // Create chat completion options
            var chatCompletionsOptions = new ChatCompletionsOptions()
            {
                DeploymentName = deploymentName,
                Temperature = 0.7f,
                MaxTokens = 800
            };
            
            // Add system message
            chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage("You are a helpful assistant."));
            
            // Add user message
            chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(request.Message));

            // Create the chat completion request
            var response = await client.GetChatCompletionsAsync(chatCompletionsOptions);

            if (response != null && response.Value.Choices.Count > 0)
            {
                var responseMessage = response.Value.Choices[0].Message.Content;
                _logger.LogInformation("Chat completion successful");
                return new ChatResponse { Response = responseMessage };
            }
            else
            {
                _logger.LogWarning("No response received from OpenAI");
                return StatusCode(500, new ChatResponse { Response = "No response received from AI service." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return StatusCode(500, new ChatResponse { Response = $"An error occurred while processing your request: {ex.Message}" });
        }
    }
}
