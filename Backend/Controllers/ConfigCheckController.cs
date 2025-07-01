using Microsoft.AspNetCore.Mvc;
using System;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigCheckController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigCheckController> _logger;

    public ConfigCheckController(IConfiguration configuration, ILogger<ConfigCheckController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<object> CheckConfiguration()
    {
        try
        {
            // Check all required configuration values
            var configCheck = new
            {
                // Use exact same casing and access pattern as ChatController
                OPENAI_API_KEY_Set = !string.IsNullOrEmpty(_configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
                OPENAI_ENDPOINT_Set = !string.IsNullOrEmpty(_configuration["OpenAI:Endpoint"] ?? Environment.GetEnvironmentVariable("OPENAI_ENDPOINT")),
                OPENAI_DEPLOYMENT_NAME_Set = !string.IsNullOrEmpty(_configuration["OpenAI:DeploymentName"] ?? Environment.GetEnvironmentVariable("OPENAI_DEPLOYMENT_NAME")),
                OPENAI_API_VERSION_Set = !string.IsNullOrEmpty(_configuration["OpenAI:ApiVersion"] ?? Environment.GetEnvironmentVariable("OPENAI_API_VERSION")),
                AZURE_FUNCTION_URL_Set = !string.IsNullOrEmpty(_configuration["AzureFunction:Url"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL")),
                AZURE_FUNCTION_KEY_Set = !string.IsNullOrEmpty(_configuration["AzureFunction:Key"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY")),
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Not Set",
                SCM_COMMAND_IDLE_TIMEOUT = Environment.GetEnvironmentVariable("SCM_COMMAND_IDLE_TIMEOUT") ?? "Not Set"
            };
            
            return Ok(configCheck);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking configuration");
            return StatusCode(500, $"Error checking configuration: {ex.Message}");
        }
    }
}
