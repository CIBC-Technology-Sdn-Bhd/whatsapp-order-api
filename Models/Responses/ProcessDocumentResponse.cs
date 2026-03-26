using DocumentProcessingApi.Models.Enums;

namespace DocumentProcessingApi.Models.Responses
{
    public class ProcessDocumentResponse
    {
        public string FileName { get; set; } = string.Empty;
        public PdfKind PdfKind { get; set; }
        public string ExtractedText { get; set; } = string.Empty;
        public object? StructuredJson { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}