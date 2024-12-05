namespace DMSystem.Messaging
{
    public class RabbitMQSetting
    {
        public string HostName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string OcrQueue { get; set; } = string.Empty; // Queue for OCR requests
        public string OcrResultsQueue { get; set; } = string.Empty; // Queue for OCR results
    }

    public static class RabbitMQQueues
    {
        public const string OcrQueue = "ocrQueue"; // Queue for OCR requests
        public const string OcrResultsQueue = "ocrResultsQueue"; // Queue for OCR results
    }
}
