using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Backend.Services
{
    public class AzureFunctionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureFunctionService> _logger;
        private readonly HttpClient _httpClient;

        public AzureFunctionService(
            IConfiguration configuration, 
            ILogger<AzureFunctionService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task SaveConversationAsync(string userMessage, string aiResponse)
        {
            try
            {
                // Generate a unique conversation ID for each message
                var conversationId = Guid.NewGuid().ToString("D");
                
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
                
                if (string.IsNullOrEmpty(functionKey))
                {
                    _logger.LogWarning("Azure Function key not configured. Skipping conversation save.");
                    return;
                }

                // Format the conversation data according to the Azure Function's expected schema
                var messages = new[]
                {
                    new { role = "user", content = userMessage },
                    new { role = "assistant", content = aiResponse }
                };

                // Create the conversation payload with additional metadata expected by the Azure Function
                // Get user info from configuration or generate unique values
                var userId = _configuration["AzureFunction:UserId"] ?? 
                    Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_ID");
                    
                var userEmail = _configuration["AzureFunction:UserEmail"] ?? 
                    Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_EMAIL");
                
                // If not configured, generate a unique user ID with a timestamp
                if (string.IsNullOrEmpty(userId))
                {
                    userId = $"user-{DateTime.UtcNow.Ticks}";
                }
                
                // If not configured, generate a unique email with a timestamp
                if (string.IsNullOrEmpty(userEmail))
                {
                    userEmail = $"user-{DateTime.UtcNow.Ticks}@realtyts.com";
                }
                
                var conversation = new
                {
                    conversationId = conversationId,
                    userId = userId,
                    userEmail = userEmail,
                    chatType = "web",
                    messages = messages,
                    totalTokens = 0,
                    metadata = new
                    {
                        source = "web",
                        timestamp = DateTime.UtcNow.ToString("o")
                    }
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
                
                // Log detailed request information
                _logger.LogInformation("Sending conversation with ID: {ConversationId}, UserId: {UserId}, UserEmail: {UserEmail}",
                    conversationId, userId, userEmail);
                
                // Send the request
                var response = await _httpClient.SendAsync(request);
                
                // Log response status
                _logger.LogInformation("Azure Function response status: {StatusCode}", response.StatusCode);
                
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
                    
                    // Log success details
                    _logger.LogInformation("Successfully saved conversation with ID: {ConversationId}", conversationId);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the main request
                _logger.LogError(ex, "Error saving conversation: {Message}", ex.Message);
            }
        }
    }
}
