using System;
using System.Collections.Generic;

namespace Backend.Models
{
    public class DocumentInfo
    {
        public string FileName { get; set; } = "";
        public List<string> Chunks { get; set; } = new List<string>();
        public string Summary { get; set; } = "";
        public DateTime UploadTime { get; set; }
        
        // For backward compatibility and simple access
        public string GetFullContent()
        {
            return string.Join("\n\n", Chunks);
        }
    }
}
