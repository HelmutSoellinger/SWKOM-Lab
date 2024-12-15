using DMSystem.Contracts.DTOs;

namespace DMSystem.Contracts
{
    public class OCRResult
    {
        public DocumentDTO Document { get; set; } = new DocumentDTO();
        public string OcrText { get; set; } = string.Empty; // Extracted OCR text
    }
}
