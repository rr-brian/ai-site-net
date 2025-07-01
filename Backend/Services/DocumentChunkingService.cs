using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Backend.Services
{
    public class DocumentChunkingService
    {
        private readonly ILogger<DocumentChunkingService> _logger;
        
        public DocumentChunkingService(ILogger<DocumentChunkingService> logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Splits a document into chunks of approximately the specified size
        /// </summary>
        /// <param name="text">The document text to chunk</param>
        /// <param name="maxChunkSize">Maximum size of each chunk in characters</param>
        /// <returns>List of document chunks</returns>
        public List<string> ChunkDocument(string text, int maxChunkSize = 4000)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<string>();
            }
            
            // If the text is smaller than the max chunk size, return it as a single chunk
            if (text.Length <= maxChunkSize)
            {
                return new List<string> { text };
            }
            
            var chunks = new List<string>();
            
            // Split the text into paragraphs
            var paragraphs = Regex.Split(text, @"(\r?\n){2,}").Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            
            var currentChunk = new StringBuilder();
            
            foreach (var paragraph in paragraphs)
            {
                // If adding this paragraph would exceed the chunk size, start a new chunk
                if (currentChunk.Length + paragraph.Length > maxChunkSize && currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                }
                
                // If a single paragraph is larger than the chunk size, split it into sentences
                if (paragraph.Length > maxChunkSize)
                {
                    var sentences = SplitIntoSentences(paragraph);
                    foreach (var sentence in sentences)
                    {
                        // If adding this sentence would exceed the chunk size, start a new chunk
                        if (currentChunk.Length + sentence.Length > maxChunkSize && currentChunk.Length > 0)
                        {
                            chunks.Add(currentChunk.ToString().Trim());
                            currentChunk.Clear();
                        }
                        
                        // If a single sentence is larger than the chunk size, split it by words
                        if (sentence.Length > maxChunkSize)
                        {
                            var words = sentence.Split(' ');
                            foreach (var word in words)
                            {
                                if (currentChunk.Length + word.Length + 1 > maxChunkSize && currentChunk.Length > 0)
                                {
                                    chunks.Add(currentChunk.ToString().Trim());
                                    currentChunk.Clear();
                                }
                                
                                currentChunk.Append(word).Append(" ");
                            }
                        }
                        else
                        {
                            currentChunk.Append(sentence).Append(" ");
                        }
                    }
                }
                else
                {
                    currentChunk.AppendLine(paragraph);
                    currentChunk.AppendLine(); // Add a blank line between paragraphs
                }
            }
            
            // Add the last chunk if it's not empty
            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }
            
            _logger.LogInformation("Document chunked into {ChunkCount} chunks", chunks.Count);
            return chunks;
        }
        
        /// <summary>
        /// Splits text into sentences
        /// </summary>
        private List<string> SplitIntoSentences(string text)
        {
            // Simple sentence splitting - this could be improved with NLP libraries
            var sentenceRegex = new Regex(@"(\.|\?|\!)\s+");
            var sentences = sentenceRegex.Split(text)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select((s, i) => i % 2 == 0 ? s : s + " ") // Add back the punctuation
                .ToList();
                
            var result = new List<string>();
            var currentSentence = new StringBuilder();
            
            foreach (var part in sentences)
            {
                currentSentence.Append(part);
                
                // If this part ends with punctuation, add it to the result
                if (part.EndsWith(". ") || part.EndsWith("? ") || part.EndsWith("! "))
                {
                    result.Add(currentSentence.ToString());
                    currentSentence.Clear();
                }
            }
            
            // Add any remaining text
            if (currentSentence.Length > 0)
            {
                result.Add(currentSentence.ToString());
            }
            
            return result;
        }
    }
}
