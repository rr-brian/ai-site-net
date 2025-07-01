using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Services;
using Backend.Models;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatController> _logger;
    private readonly DocumentProcessingService _documentService;
    private readonly DocumentChunkingService _chunkingService;
    private readonly OpenAIService _openAIService;
    private readonly AzureFunctionService _azureFunctionService;

    public ChatController(
        IConfiguration configuration, 
        ILogger<ChatController> logger,
        DocumentProcessingService documentService,
        DocumentChunkingService chunkingService,
        OpenAIService openAIService,
        AzureFunctionService azureFunctionService)
    {
        _configuration = configuration;
        _logger = logger;
        _documentService = documentService;
        _chunkingService = chunkingService;
        _openAIService = openAIService;
        _azureFunctionService = azureFunctionService;
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
            // Check if there's a document in the session
            DocumentInfo? documentInfo = null;
            string? documentSession = HttpContext.Session.GetString("CurrentDocument");
            if (!string.IsNullOrEmpty(documentSession))
            {
                try
                {
                    documentInfo = JsonSerializer.Deserialize<DocumentInfo>(documentSession);
                    _logger.LogInformation("Found document in session: {FileName}", documentInfo?.FileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing document from session");
                }
            }
            
            string userMessage = request.Message;
            
            // If we have a document, include relevant chunks in the context
            if (documentInfo != null && documentInfo.Chunks.Any())
            {
                // First, check if we have a summary to provide context
                string contextPrefix = "";
                if (!string.IsNullOrEmpty(documentInfo.Summary))
                {
                    contextPrefix = $"Document summary: {documentInfo.Summary}\n\n";
                }
                
                // Find the most relevant chunks for this query
                // For now, we'll use a simple approach of including the first 2-3 chunks
                // In a more advanced implementation, we could use embeddings to find the most relevant chunks
                
                int maxChunksToInclude = Math.Min(3, documentInfo.Chunks.Count);
                var relevantChunks = documentInfo.Chunks.Take(maxChunksToInclude).ToList();
                
                string chunksText = string.Join("\n\n---\n\n", relevantChunks);
                
                // Create a prompt that includes both the document content and the user's question
                userMessage = $"Based on the document named '{documentInfo.FileName}', here are the relevant sections:\n\n{contextPrefix}{chunksText}\n\nThe user asks: {request.Message}";
                _logger.LogInformation("Added document context to message with {ChunkCount} chunks", relevantChunks.Count);
            }
            
            // Get chat completion using the OpenAI service
            string responseMessage = await _openAIService.GetChatCompletionAsync(userMessage, null);
            
            // Save conversation if it's not a document-based query
            if (documentInfo == null)
            {
                // Fire and forget - don't await
                _ = _azureFunctionService.SaveConversationAsync(request.Message, responseMessage);
            }
            
            // Return the response
            return new ChatResponse { Response = responseMessage };
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
            // Try different ways to access environment variables
            var directApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var configApiKey = _configuration["OpenAI:ApiKey"];
            
            // Create dictionaries to hold the values
            var directEnvVars = new Dictionary<string, string?>();
            var allEnvVars = new Dictionary<string, string?>();
            var allConfigValues = new Dictionary<string, string?>();
            
            // Add the OpenAI specific values
            directEnvVars["OPENAI_API_KEY"] = directApiKey != null ? "[REDACTED]" : null;
            directEnvVars["OPENAI_ENDPOINT"] = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");
            directEnvVars["OPENAI_DEPLOYMENT_NAME"] = Environment.GetEnvironmentVariable("OPENAI_DEPLOYMENT_NAME");
            directEnvVars["OPENAI_API_VERSION"] = Environment.GetEnvironmentVariable("OPENAI_API_VERSION");
            directEnvVars["AZURE_FUNCTION_URL"] = Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL");
            directEnvVars["AZURE_FUNCTION_KEY"] = Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY") != null ? "[REDACTED]" : null;
            directEnvVars["SCM_COMMAND_IDLE_TIMEOUT"] = Environment.GetEnvironmentVariable("SCM_COMMAND_IDLE_TIMEOUT");
            
            // Get all environment variables (excluding secrets)
            foreach (System.Collections.DictionaryEntry de in Environment.GetEnvironmentVariables())
            {
                string? key = de.Key?.ToString();
                if (key != null && !key.Contains("SECRET") && !key.Contains("KEY") && !key.Contains("PASSWORD"))
                {
                    allEnvVars[key] = de.Value?.ToString() ?? string.Empty;
                }
            }
            
            // Get all configuration values (excluding secrets)
            foreach (var config in _configuration.AsEnumerable())
            {
                if (!config.Key.Contains("Secret") && !config.Key.Contains("Key") && !config.Key.Contains("Password"))
                {
                    allConfigValues[config.Key] = config.Value ?? string.Empty;
                }
            }
            
            // Return all the values
            return new
            {
                DirectEnvironmentVariables = directEnvVars,
                AllEnvironmentVariables = allEnvVars,
                ConfigurationValues = allConfigValues
            };
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("with-document")]
    public async Task<IActionResult> ChatWithDocument(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }
        
        // Check file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".pdf" && extension != ".docx" && extension != ".xlsx")
        {
            return BadRequest("Unsupported file format. Please upload a PDF, Word, or Excel file.");
        }
        
        try
        {
            // Extract text from the document using the document service
            string documentText = await _documentService.ExtractTextFromDocument(file);
            
            if (string.IsNullOrEmpty(documentText))
            {
                return BadRequest("Could not extract text from the document");
            }
            
            // Chunk the document text into manageable pieces
            var documentChunks = _chunkingService.ChunkDocument(documentText, 2000); // 2000 chars per chunk
            
            if (documentChunks.Count == 0)
            {
                return BadRequest("Could not process the document content");
            }
            
            _logger.LogInformation("Document chunked into {ChunkCount} chunks", documentChunks.Count);
            
            // Create a prompt to generate a summary of the document
            // We'll use the first chunk or combine a few chunks if the document is small
            string summaryText = documentChunks.Count <= 3 
                ? string.Join("\n\n", documentChunks) 
                : string.Join("\n\n", documentChunks.Take(3));
                
            string summaryPrompt = $"The following is content from a document named '{file.FileName}'. Please analyze it and provide a concise summary: \n\n{summaryText}";
            
            // Get summary from OpenAI
            var summary = await _openAIService.GetChatCompletionAsync(summaryPrompt, null);
            
            // Store document info in session
            var documentInfo = new DocumentInfo
            {
                FileName = file.FileName,
                Chunks = documentChunks,
                Summary = summary,
                UploadTime = DateTime.Now
            };
            
            // Store in session
            HttpContext.Session.SetString("CurrentDocument", JsonSerializer.Serialize(documentInfo));
            
            _logger.LogInformation("Document stored in session with {ChunkCount} chunks and summary", documentChunks.Count);
            
            return Ok(new { response = summary, documentStored = true, chunkCount = documentChunks.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document {FileName}", file.FileName);
            return StatusCode(500, new { error = "Error processing document" });
        }
    }
    
    [HttpPost("with-file")]
    public async Task<IActionResult> ChatWithFile(IFormFile file, [FromForm] string message)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }
        
        if (string.IsNullOrEmpty(message))
        {
            return BadRequest("No message provided");
        }
        
        _logger.LogInformation("Received chat with file request. File: {FileName}, Message: {Message}", file.FileName, message);
        
        // Check file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".pdf" && extension != ".docx" && extension != ".xlsx")
        {
            return BadRequest("Unsupported file format. Please upload a PDF, Word, or Excel file.");
        }
        
        try
        {
            // Extract text from the document
            string documentText = await _documentService.ExtractTextFromDocument(file);
            
            if (string.IsNullOrEmpty(documentText))
            {
                return BadRequest("Could not extract text from the document");
            }
            
            // Chunk the document text into manageable pieces
            var documentChunks = _chunkingService.ChunkDocument(documentText, 2000);
            
            if (documentChunks.Count == 0)
            {
                return BadRequest("Could not process the document content");
            }
            
            _logger.LogInformation("Document chunked into {ChunkCount} chunks for direct query", documentChunks.Count);
            
            // Create a combined prompt with document content and user's question
            // For direct queries, we'll use more chunks if available to provide better context
            string documentContent;
            if (documentChunks.Count <= 5)
            {
                // For smaller documents, include all chunks
                documentContent = string.Join("\n\n---\n\n", documentChunks);
            }
            else
            {
                // For larger documents, include first few chunks
                documentContent = string.Join("\n\n---\n\n", documentChunks.Take(5));
            }
            
            // Create prompt with document content and user question
            string prompt = $"The following is content from a document named '{file.FileName}':\n\n{documentContent}\n\nBased on this document, please answer the following question: {message}";
            
            // Get completion from OpenAI
            var response = await _openAIService.GetChatCompletionAsync(prompt, null);
            
            // We don't store this document in session since it's a one-time query
            
            return Ok(new { response = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document with message. File: {FileName}", file.FileName);
            return StatusCode(500, new { error = "Error processing document with message" });
        }
    }

    [HttpPost("clear-document")]
    public IActionResult ClearDocumentContext()
    {
        try
        {
            // Remove document from session
            HttpContext.Session.Remove("CurrentDocument");
            _logger.LogInformation("Document context cleared from session");
            
            return Ok(new { success = true, message = "Document context cleared" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing document context");
            return StatusCode(500, new { error = "Error clearing document context" });
        }
    }
    
    public class ChatHistoryRequest
    {
        [JsonPropertyName("messages")]
        public List<ChatHistoryMessage> Messages { get; set; } = new List<ChatHistoryMessage>();
    }
    
    public class ChatHistoryMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";
        
        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
        
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = "";
    }
    
    [HttpPost("download-history")]
    public IActionResult DownloadChatHistory([FromBody] ChatHistoryRequest request)
    {
        try
        {
            _logger.LogInformation("Received download history request");
            
            // Create a JSON object with metadata and messages
            var chatData = new
            {
                metadata = new
                {
                    title = "RAI Chat Transcript",
                    generated = DateTime.UtcNow.ToString("o"),
                    version = "1.0"
                },
                messages = request.Messages
            };
            
            // Serialize to JSON with indentation
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            string jsonContent = JsonSerializer.Serialize(chatData, jsonOptions);
            
            // Create a memory stream with the JSON content
            var stream = new System.IO.MemoryStream();
            var writer = new System.IO.StreamWriter(stream);
            writer.Write(jsonContent);
            writer.Flush();
            stream.Position = 0;
            
            // Return as a file download
            string fileName = $"rai-chat-{DateTime.UtcNow.ToString("yyyy-MM-dd")}.json";
            return File(stream, "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating chat history download");
            return StatusCode(500, new { error = "Error generating chat history download" });
        }
    }
    
    // Alternative endpoint that accepts form data for better browser compatibility
    [HttpPost("download-history"), Consumes("application/x-www-form-urlencoded")]
    public IActionResult DownloadChatHistoryForm([FromForm] string messages)
    {
        try
        {
            _logger.LogInformation("Received form-based download history request");
            
            // Deserialize the messages from the form data
            ChatHistoryRequest? request = null;
            
            try {
                request = JsonSerializer.Deserialize<ChatHistoryRequest>(messages);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error deserializing messages from form");
                // Create a simple text file as fallback
                var textStream = new System.IO.MemoryStream();
                var textWriter = new System.IO.StreamWriter(textStream);
                textWriter.Write("RAI Chat Transcript\r\nError processing messages. Using raw data.\r\n\r\n" + messages);
                textWriter.Flush();
                textStream.Position = 0;
                
                return File(textStream, "text/plain", $"rai-chat-{DateTime.UtcNow.ToString("yyyy-MM-dd")}.txt");
            }
            
            if (request == null || request.Messages == null || !request.Messages.Any())
            {
                _logger.LogWarning("No messages found in download request");
                var emptyStream = new System.IO.MemoryStream();
                var emptyWriter = new System.IO.StreamWriter(emptyStream);
                emptyWriter.Write("RAI Chat Transcript\r\nNo messages found.\r\n");
                emptyWriter.Flush();
                emptyStream.Position = 0;
                
                return File(emptyStream, "text/plain", $"rai-chat-{DateTime.UtcNow.ToString("yyyy-MM-dd")}.txt");
            }
            
            // Create a JSON object with metadata and messages
            var chatData = new
            {
                metadata = new
                {
                    title = "RAI Chat Transcript",
                    generated = DateTime.UtcNow.ToString("o"),
                    version = "1.0"
                },
                messages = request.Messages
            };
            
            // Serialize to JSON with indentation
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            string jsonContent = JsonSerializer.Serialize(chatData, jsonOptions);
            
            // Create a memory stream with the JSON content
            var stream = new System.IO.MemoryStream();
            var writer = new System.IO.StreamWriter(stream);
            writer.Write(jsonContent);
            writer.Flush();
            stream.Position = 0;
            
            // Return as a file download
            string fileName = $"rai-chat-{DateTime.UtcNow.ToString("yyyy-MM-dd")}.json";
            return File(stream, "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating chat history download from form");
            
            // Return a simple error text file as fallback
            var errorStream = new System.IO.MemoryStream();
            var errorWriter = new System.IO.StreamWriter(errorStream);
            errorWriter.Write("RAI Chat Transcript\r\nAn error occurred while generating the download.\r\n");
            errorWriter.Flush();
            errorStream.Position = 0;
            
            return File(errorStream, "text/plain", $"rai-chat-error-{DateTime.UtcNow.ToString("yyyy-MM-dd")}.txt");
        }
    }
    
    [HttpGet("test-function")]
    public async Task<IActionResult> TestAzureFunction()
    {
        try
        {
            // Get Azure Function configuration
            var functionUrl = _configuration["AzureFunction:Url"] ?? 
                Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL");
            
            var functionKey = _configuration["AzureFunction:Key"] ?? 
                Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY");
                
            // Check if configuration is missing
            if (string.IsNullOrEmpty(functionUrl))
            {
                return BadRequest("Azure Function URL not configured");
            }
            
            if (string.IsNullOrEmpty(functionKey))
            {
                return BadRequest("Azure Function key not configured");
            }
            
            // Create a test conversation
            var testConversation = new
            {
                conversationId = Guid.NewGuid().ToString("D"),
                userId = "test-user",
                userEmail = "test@example.com",
                chatType = "test",
                messages = new[]
                {
                    new { role = "user", content = "This is a test message" },
                    new { role = "assistant", content = "This is a test response" }
                },
                totalTokens = 0,
                metadata = new
                {
                    source = "test",
                    timestamp = DateTime.UtcNow.ToString("o")
                }
            };
            
            // Save the conversation using the Azure Function service
            await _azureFunctionService.SaveConversationAsync("This is a test message", "This is a test response");
            
            return Ok(new { message = "Test conversation sent to Azure Function" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
