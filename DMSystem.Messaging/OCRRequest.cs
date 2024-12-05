namespace DMSystem.Messaging
{
    public class OCRRequest
    {
        public string DocumentId { get; set; } = string.Empty; // Unique identifier for the document
        public string PdfUrl { get; set; } = string.Empty; // Path or URL to the PDF file
    }
}
