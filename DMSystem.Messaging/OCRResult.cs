namespace DMSystem.Messaging
{
    public class OCRResult
    {
        public string DocumentId { get; set; } = string.Empty; // Document ID for correlation
        public string OcrText { get; set; } = string.Empty; // Extracted OCR text
    }
}
