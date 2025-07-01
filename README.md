# AI Chatbot Web Application

This project is a modern, responsive chatbot web application built with .NET 9 and integrates with the Azure OpenAI API.

## Repository

This project is hosted on GitHub at: https://github.com/rr-brian/ai-site-net

## Current Status

The application is fully functional with:
- A responsive chat interface with modern UI
- Message bubbles for user and AI responses
- Input box and send button for user messages
- Backend API integration with Azure OpenAI
- Configuration for deployment to Azure App Service

## Setup Instructions

### Prerequisites

1. Install .NET 9 SDK from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
2. Azure OpenAI API access (endpoint, API key, and deployment name)

### Running the Application Locally

1. Clone the repository:
   ```
   git clone https://github.com/rr-brian/ai-site-net.git
   cd ai-site-net
   ```

2. Build and run the backend:
   ```
   cd Backend
   dotnet build
   dotnet run
   ```

3. Create a new file `ChatController.cs` in the `Controllers` directory with the following content:
   ```csharp
   using Microsoft.AspNetCore.Mvc;
   using Azure.AI.OpenAI;
   using Azure;
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
               var apiKey = _configuration["OpenAI:ApiKey"] ?? 
                   Environment.GetEnvironmentVariable("OPENAI_API_KEY");
               var endpoint = _configuration["OpenAI:Endpoint"] ?? 
                   Environment.GetEnvironmentVariable("OPENAI_ENDPOINT") ?? 
                   "https://api.openai.com/v1/chat/completions";
               var deploymentName = _configuration["OpenAI:DeploymentName"] ?? 
                   Environment.GetEnvironmentVariable("OPENAI_DEPLOYMENT_NAME") ?? 
                   "gpt-4";
   
               if (string.IsNullOrEmpty(apiKey))
               {
                   _logger.LogError("OpenAI API key not found");
                   return BadRequest("OpenAI API key not configured");
               }
   
               OpenAIClient client;
               if (endpoint.Contains("openai.azure.com"))
               {
                   // Azure OpenAI
                   client = new OpenAIClient(
                       new Uri(endpoint),
                       new AzureKeyCredential(apiKey));
               }
               else
               {
                   // OpenAI
                   client = new OpenAIClient(apiKey);
               }
   
               var chatCompletionsOptions = new ChatCompletionsOptions
               {
                   Messages =
                   {
                       new ChatMessage(ChatRole.System, "You are a helpful assistant."),
                       new ChatMessage(ChatRole.User, request.Message)
                   },
                   MaxTokens = 800
               };
   
               var response = await client.GetChatCompletionsAsync(deploymentName, chatCompletionsOptions);
               var responseMessage = response.Value.Choices[0].Message.Content;
   
               return new ChatResponse { Response = responseMessage };
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Error processing chat request");
               return StatusCode(500, new ChatResponse { Response = "An error occurred while processing your request." });
           }
       }
   }
   ```

4. Update `appsettings.json` to include OpenAI configuration:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft.AspNetCore": "Warning"
       }
     },
     "AllowedHosts": "*",
     "OpenAI": {
       "ApiKey": "",
       "Endpoint": "https://api.openai.com/v1/chat/completions",
       "DeploymentName": "gpt-4"
     }
   }
   ```

5. Update `Program.cs` to enable CORS and configure the application:
   ```csharp
   var builder = WebApplication.CreateBuilder(args);

   // Add services to the container.
   builder.Services.AddControllers();
   builder.Services.AddEndpointsApiExplorer();
   builder.Services.AddSwaggerGen();

   // Add CORS policy
   builder.Services.AddCors(options =>
   {
       options.AddPolicy("AllowAll", builder =>
       {
           builder.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
       });
   });

   var app = builder.Build();

   // Configure the HTTP request pipeline.
   if (app.Environment.IsDevelopment())
   {
       app.UseSwagger();
       app.UseSwaggerUI();
   }

   app.UseStaticFiles();
   app.UseCors("AllowAll");
   app.UseAuthorization();
   app.MapControllers();

   app.Run();
   ```

6. Start the backend server:
   ```
   dotnet run
   ```

### Setting Environment Variables

For security, set your OpenAI API key as an environment variable:

```
# Windows
setx OPENAI_API_KEY "your-api-key-here"

# Linux/macOS
export OPENAI_API_KEY="your-api-key-here"
```

## Deployment

### Deploying to GitHub

Use the included PowerShell script to deploy to GitHub:

```powershell
.\deploy.ps1
```

This will initialize a Git repository (if needed), add all files, commit them, and push to the GitHub repository.

### Deploying to Azure App Service

1. Create the necessary Azure resources:
   ```
   az group create --name rg-innovation --location eastus
   az appservice plan create --name plan-site-net --resource-group rg-innovation --sku B1 --is-linux
   az webapp create --resource-group rg-innovation --plan plan-site-net --name site-net --runtime "DOTNET|9.0"
   ```

2. Configure the application settings:
   ```
   az webapp config appsettings set --resource-group rg-innovation --name site-net --settings OPENAI_API_KEY="your-api-key-here" OPENAI_ENDPOINT="your-endpoint-here" OPENAI_DEPLOYMENT_NAME="your-deployment-name" OPENAI_API_VERSION="your-api-version"
   ```

3. Deploy the application:
   ```
   dotnet publish -c Release
   az webapp deployment source config-zip --resource-group rg-innovation --name site-net --src ./bin/Release/net9.0/publish/site-net.zip
   ```

Alternatively, you can set up GitHub Actions for continuous deployment from your GitHub repository.
