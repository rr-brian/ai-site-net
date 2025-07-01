using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System;
using System.Linq;

namespace Backend.Services
{
    public class DocumentProcessingService
    {
        private readonly ILogger<DocumentProcessingService> _logger;

        public DocumentProcessingService(ILogger<DocumentProcessingService> logger)
        {
            _logger = logger;
        }

        public async Task<string> ExtractTextFromDocument(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return string.Empty;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            try
            {
                switch (extension)
                {
                    case ".pdf":
                        return await ExtractTextFromPdf(file);
                    case ".docx":
                        return await ExtractTextFromWord(file);
                    case ".xlsx":
                        return await ExtractTextFromExcel(file);
                    default:
                        _logger.LogWarning("Unsupported file format: {Extension}", extension);
                        return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from {FileName}", file.FileName);
                return string.Empty;
            }
        }

        private async Task<string> ExtractTextFromPdf(IFormFile file)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            
            var text = new StringBuilder();
            using var pdfReader = new PdfReader(memoryStream);
            using var pdfDocument = new PdfDocument(pdfReader);
            
            for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
            {
                var strategy = new SimpleTextExtractionStrategy();
                var currentText = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(i), strategy);
                text.Append(currentText);
            }
            
            return text.ToString();
        }

        private async Task<string> ExtractTextFromWord(IFormFile file)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            
            using var wordDoc = WordprocessingDocument.Open(memoryStream, false);
            var text = wordDoc.MainDocumentPart.Document.Body.InnerText;
            
            return text;
        }

        private async Task<string> ExtractTextFromExcel(IFormFile file)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            
            using var spreadsheetDocument = SpreadsheetDocument.Open(memoryStream, false);
            var workbookPart = spreadsheetDocument.WorkbookPart;
            var sharedStringTablePart = workbookPart.SharedStringTablePart;
            var sharedStringTable = sharedStringTablePart?.SharedStringTable;
            
            var text = new StringBuilder();
            
            foreach (var sheet in workbookPart.WorksheetParts)
            {
                var worksheet = sheet.Worksheet;
                var sheetData = worksheet.Elements<SheetData>().First();
                
                foreach (var row in sheetData.Elements<Row>())
                {
                    foreach (var cell in row.Elements<Cell>())
                    {
                        string cellValue = GetCellValue(cell, sharedStringTable);
                        if (!string.IsNullOrEmpty(cellValue))
                        {
                            text.Append(cellValue + " ");
                        }
                    }
                    text.AppendLine();
                }
                text.AppendLine();
            }
            
            return text.ToString();
        }

        private string GetCellValue(Cell cell, SharedStringTable sharedStringTable)
        {
            if (cell.CellValue == null)
                return string.Empty;
                
            string value = cell.CellValue.InnerText;
            
            // If the cell represents a numeric value
            if (cell.DataType == null || cell.DataType.Value != CellValues.SharedString)
                return value;
                
            // If the cell represents a shared string
            if (sharedStringTable != null && int.TryParse(value, out int index) && index >= 0 && index < sharedStringTable.Count())
                return sharedStringTable.ElementAt(index).InnerText;
                
            return string.Empty;
        }
    }
}
