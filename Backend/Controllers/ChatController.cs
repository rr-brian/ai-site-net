using Microsoft.AspNetCore.Mvc;
using Azure;
using Azure.AI.OpenAI;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatController> _logger;
    private readonly HttpClient _httpClient;

    public ChatController(IConfiguration configuration, ILogger<ChatController> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
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
            
            // Add system message with RAI branding
            chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(
                "You are RAI, a helpful AI assistant. Respond in a friendly, concise, and helpful manner. " +
                "Sign your responses as 'RAI - Your AI Assistant'."));
            
            // Add user message
            chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(request.Message));

            // Create the chat completion request
            var response = await client.GetChatCompletionsAsync(chatCompletionsOptions);

            if (response != null && response.Value.Choices.Count > 0)
            {
                var responseMessage = response.Value.Choices[0].Message.Content;
                _logger.LogInformation("Chat completion successful");
                
                // Save conversation asynchronously (don't await to avoid blocking the response)
                _ = Task.Run(() => SaveConversationAsync(request.Message, responseMessage));
                
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

    // Test endpoint for Azure Function diagnostics
    [HttpGet("env-check")]
    public ActionResult<object> CheckEnvironmentVariables()
    {
        try
        {
            // Get the actual values (with sensitive parts redacted)
            var apiKey = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var endpoint = _configuration["OpenAI:Endpoint"] ?? Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");
            var deploymentName = _configuration["OpenAI:DeploymentName"] ?? Environment.GetEnvironmentVariable("OPENAI_DEPLOYMENT_NAME");
            var apiVersion = _configuration["OpenAI:ApiVersion"] ?? Environment.GetEnvironmentVariable("OPENAI_API_VERSION");
            var functionUrl = _configuration["AzureFunction:Url"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL");
            var functionKey = _configuration["AzureFunction:Key"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY");
            
            return Ok(new
            {
                OPENAI_API_KEY = !string.IsNullOrEmpty(apiKey) ? "[REDACTED]" : null,
                OPENAI_ENDPOINT = endpoint,
                OPENAI_DEPLOYMENT_NAME = deploymentName,
                OPENAI_API_VERSION = apiVersion,
                AZURE_FUNCTION_URL = functionUrl,
                AZURE_FUNCTION_KEY = !string.IsNullOrEmpty(functionKey) ? "[REDACTED]" : null,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                AllEnvironmentVariables = Environment.GetEnvironmentVariables()
                    .Cast<System.Collections.DictionaryEntry>()
                    .Where(e => !e.Key.ToString().Contains("SECRET") && 
                                !e.Key.ToString().Contains("KEY") && 
                                !e.Key.ToString().Contains("PASSWORD"))
                    .ToDictionary(e => e.Key.ToString(), e => e.Value?.ToString())
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking environment variables");
            return StatusCode(500, $"Error checking environment variables: {ex.Message}");
        }
    }

    [HttpGet("test-function")]
    public async Task<IActionResult> TestAzureFunction()
    {
        try
        {
            var functionUrl = _configuration["AzureFunction:Url"] ?? 
                Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL");
            
            var functionKey = _configuration["AzureFunction:Key"] ?? 
                Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY");

            _logger.LogInformation("Azure Function URL: {Url}", functionUrl);
            _logger.LogInformation("Function key available: {HasKey}", !string.IsNullOrEmpty(functionKey));
            
            // Check if configuration is missing
            if (string.IsNullOrEmpty(functionUrl))
            {
                return BadRequest(new { Error = "Azure Function URL not configured. Please set the AZURE_FUNCTION_URL environment variable." });
            }

            // Create a minimal test payload
            var testPayload = new
            {
                conversationId = "test-conversation-" + Guid.NewGuid().ToString(),
                userId = "test-user",
                userEmail = "test@example.com",
                chatType = "test",
                messages = new[] 
                { 
                    new { role = "user", content = "Test message" },
                    new { role = "assistant", content = "Test response" }
                },
                totalTokens = 0,
                metadata = new { source = "test", timestamp = DateTime.UtcNow.ToString("o") }
            };

            // Build the URL with the function key
            var requestUri = functionUrl;
            if (!string.IsNullOrEmpty(functionKey) && !requestUri.Contains("code="))
            {
                requestUri = requestUri + (requestUri.Contains("?") ? "&" : "?") + "code=" + functionKey;
            }

            // Log the request details
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            var requestJson = JsonSerializer.Serialize(testPayload, jsonOptions);
            _logger.LogInformation("Test request payload: {Payload}", requestJson);

            // Create a custom HttpRequestMessage for more control
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            
            // Add any additional headers that might be needed
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Send the request
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Return all diagnostic information
            return Ok(new
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = response.StatusCode,
                ResponseContent = responseContent,
                RequestUrl = requestUri.Replace(functionKey ?? "", "[REDACTED]"),
                RequestPayload = testPayload
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Azure Function");
            return StatusCode(500, new { Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    private async Task SaveConversationAsync(string userMessage, string aiResponse)
    {
        try
        {
            // Generate a unique conversation ID
            var conversationId = Guid.NewGuid().ToString();
            
            // Get Azure Function configuration
            var functionUrl = _configuration["AzureFunction:Url"] ?? 
                Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL");
            
            var functionKey = _configuration["AzureFunction:Key"] ?? 
                Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY");
                
            // Skip if configuration is missing
            if (string.IsNullOrEmpty(functionUrl))
            {
                _logger.LogWarning("Azure Function URL not configured. Skipping conversation save.");
                return;
            }

            // Format the conversation data according to the Azure Function's expected schema
            var messages = new[]
            {
                new { role = "user", content = userMessage },
                new { role = "assistant", content = aiResponse }
            };

            // Create the conversation payload
            var conversation = new
            {
                conversationId = conversationId,
                userId = "web-user",
                userEmail = "anonymous@example.com",
                messages = messages
            };

            // Build the URL with the function key
            var requestUri = functionUrl;
            if (!string.IsNullOrEmpty(functionKey) && !requestUri.Contains("code="))
            {
                requestUri = requestUri + (requestUri.Contains("?") ? "&" : "?") + "code=" + functionKey;
            }

            _logger.LogInformation("Sending conversation to Azure Function at: {Uri}", 
                requestUri.Replace(functionKey ?? "", "[REDACTED]"));
            
            // Serialize the conversation to JSON
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            var requestJson = JsonSerializer.Serialize(conversation, jsonOptions);
            _logger.LogInformation("Request payload: {Payload}", requestJson);
            
            // Create request with the properly formatted URL
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
            
            // Send the request
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to save conversation. Status code: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
            }
            else
            {
                var successContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Conversation saved successfully. Response: {Response}", successContent);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the main request
            _logger.LogError(ex, "Error saving conversation: {Message}", ex.Message);
        }
    }
}
