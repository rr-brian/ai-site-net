using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Backend.Services
{
    public class OpenAIService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenAIService> _logger;

        public OpenAIService(IConfiguration configuration, ILogger<OpenAIService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> GetChatCompletionAsync(string message, string? conversationId)
        {
            // Get configuration values - try multiple approaches to access environment variables
            string? apiKey = null;
            string? endpoint = null;
            string? deploymentName = null;
            string? apiVersion = null;
            
            // Try direct environment variables first
            apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");
            deploymentName = Environment.GetEnvironmentVariable("OPENAI_DEPLOYMENT_NAME");
            apiVersion = Environment.GetEnvironmentVariable("OPENAI_API_VERSION");
            
            // If not found, try configuration
            if (string.IsNullOrEmpty(apiKey))
                apiKey = _configuration["OpenAI:ApiKey"];
                
            if (string.IsNullOrEmpty(endpoint))
                endpoint = _configuration["OpenAI:Endpoint"];
                
            if (string.IsNullOrEmpty(deploymentName))
                deploymentName = _configuration["OpenAI:DeploymentName"];
                
            if (string.IsNullOrEmpty(apiVersion))
                apiVersion = _configuration["OpenAI:ApiVersion"];
                
            // Log what we found
            _logger.LogInformation($"API Key found: {!string.IsNullOrEmpty(apiKey)}");
            _logger.LogInformation($"Endpoint: {endpoint}");
            _logger.LogInformation($"Deployment: {deploymentName}");
            _logger.LogInformation($"API Version: {apiVersion}");

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint) || 
                string.IsNullOrEmpty(deploymentName) || string.IsNullOrEmpty(apiVersion))
            {
                _logger.LogError("OpenAI configuration incomplete. Missing one or more required values.");
                return "I'm sorry, but I'm not configured correctly. Please check the OpenAI configuration settings.";
            }

            try
            {
                // Initialize the OpenAI client
                var openAIClient = new OpenAIClient(
                    new Uri(endpoint),
                    new AzureKeyCredential(apiKey));
                
                // Create chat completion options
                var chatCompletionOptions = new ChatCompletionsOptions
                {
                    DeploymentName = deploymentName,
                    Messages =
                    {
                        new ChatRequestSystemMessage("You are RAI, a helpful AI assistant. You are knowledgeable, friendly, and able to assist with a wide range of topics."),
                        new ChatRequestUserMessage(message)
                    },
                    MaxTokens = 800,
                    Temperature = 0.7f,
                    NucleusSamplingFactor = 0.95f,
                    FrequencyPenalty = 0,
                    PresencePenalty = 0
                };
                
                // Get completion from Azure OpenAI
                var chatCompletionsResponse = await openAIClient.GetChatCompletionsAsync(chatCompletionOptions);
                var responseMessage = chatCompletionsResponse.Value.Choices[0].Message.Content;
                
                return responseMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API");
                return $"I'm sorry, but I encountered an error: {ex.Message}";
            }
        }
    }
}
