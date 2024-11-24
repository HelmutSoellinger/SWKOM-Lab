namespace DMSystem.Messaging
{
    public class OCRRequest
    {
        public string DocumentId { get; set; } = string.Empty;
        public string PdfUrl { get; set; } = string.Empty;
    }
}
