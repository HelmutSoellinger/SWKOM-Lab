using Xunit;
using Moq;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DMSystem.Contracts;
using DMSystem.Minio;
using DMSystem.OCRWorker;
using DMSystem.Contracts.DTOs;

namespace DMSystem.Tests.OCRWorker
{
    public class OCRWorkerTests
    {
        private readonly Mock<IOptions<RabbitMQSettings>> _mockRabbitMqOptions;
        private readonly Mock<IMinioFileStorageService> _mockFileStorageService;
        private readonly Mock<IRabbitMQService> _mockRabbitMqService;
        private readonly Mock<ILogger<Worker>> _mockLogger;

        public OCRWorkerTests()
        {
            _mockRabbitMqOptions = new Mock<IOptions<RabbitMQSettings>>();
            _mockFileStorageService = new Mock<IMinioFileStorageService>();
            _mockRabbitMqService = new Mock<IRabbitMQService>();
            _mockLogger = new Mock<ILogger<Worker>>();

            // Set up RabbitMQ settings
            var rabbitMqSettings = new RabbitMQSettings
            {
                Queues = new Dictionary<string, string>
                {
                    { "OcrQueue", "ocr_queue" },
                    { "OcrResultsQueue", "ocr_results_queue" }
                }
            };
            _mockRabbitMqOptions.Setup(o => o.Value).Returns(rabbitMqSettings);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldProcessOCRRequests()
        {
            // Arrange
            var worker = new Worker(
                _mockRabbitMqOptions.Object,
                _mockFileStorageService.Object,
                _mockRabbitMqService.Object,
                _mockLogger.Object
            );

            var ocrRequest = new OCRRequest
            {
                Document = new DocumentDTO
                {
                    Id = 123,
                    FilePath = "test.pdf"
                }
            };

            // Simulate consuming an OCR request
            _mockRabbitMqService.Setup(m => m.ConsumeQueue(
                It.Is<string>(q => q == "ocr_queue"),
                It.IsAny<Func<OCRRequest, Task>>()
            )).Callback<string, Func<OCRRequest, Task>>((queue, callback) =>
            {
                callback(ocrRequest).Wait();
            });

            // Mock file download behavior
            var mockFileStream = new MemoryStream(Encoding.UTF8.GetBytes("Test PDF Content"));
            _mockFileStorageService.Setup(f => f.DownloadFileAsync(It.IsAny<string>()))
                .ReturnsAsync(mockFileStream);

            // Mock result publishing
            _mockRabbitMqService.Setup(m => m.PublishMessageAsync(
                It.IsAny<OCRResult>(),
                It.Is<string>(q => q == "ocr_results_queue")
            )).Returns(Task.CompletedTask);

            // Act
            var cts = new CancellationTokenSource();
            cts.CancelAfter(1000); // Stop the worker after some time
            await worker.StartAsync(cts.Token);

            // Assert
            _mockFileStorageService.Verify(f => f.DownloadFileAsync("test.pdf"), Times.Once);
            _mockRabbitMqService.Verify(m => m.PublishMessageAsync(It.IsAny<OCRResult>(), "ocr_results_queue"), Times.Once);
        }

        [Fact]
        public async Task PerformOcrAsync_ShouldExtractTextFromValidPdf()
        {
            // Arrange
            var filePath = "sample.pdf"; // Platzieren Sie eine PDF-Datei in Ihrem Arbeitsverzeichnis.
            var worker = new Worker(
                _mockRabbitMqOptions.Object,
                _mockFileStorageService.Object,
                _mockRabbitMqService.Object,
                _mockLogger.Object
            );

            // Mock the file storage service to simulate a downloaded PDF
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            _mockFileStorageService.Setup(f => f.DownloadFileAsync(It.IsAny<string>())).ReturnsAsync(fileStream);

            // Act
            var result = await worker.PerformOcrAsync(filePath);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(result), "OCR result should not be empty.");
            Assert.Contains("Expected text in the PDF", result); // Optional: Prüfen Sie auf spezifischen Text
        }

        [Fact]
        public async Task ExecuteAsync_ShouldHandleInvalidMessagesGracefully()
        {
            // Arrange
            var worker = new Worker(
                _mockRabbitMqOptions.Object,
                _mockFileStorageService.Object,
                _mockRabbitMqService.Object,
                _mockLogger.Object
            );

            // Simulate invalid message
            var invalidRequest = "Invalid JSON";

            _mockRabbitMqService.Setup(m => m.ConsumeQueue(
                It.Is<string>(q => q == "ocr_queue"),
                It.IsAny<Func<OCRRequest, Task>>()
            )).Callback<string, Func<OCRRequest, Task>>(async (queue, callback) =>
            {
                try
                {
                    // Simulate deserialization failure
                    await callback(JsonSerializer.Deserialize<OCRRequest>(invalidRequest));
                }
                catch (Exception ex)
                {
                    _mockLogger.Object.LogError(ex, "Error occurred while handling message.");
                }
            });

            // Act
            var cts = new CancellationTokenSource();
            cts.CancelAfter(1000); // Stop the worker after some time
            await worker.StartAsync(cts.Token);

            // Assert
            _mockLogger.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<object>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<object, Exception, string>>()
            ), Times.AtLeastOnce);
        }

        [Fact]
        public async Task SendResultAsync_ShouldPublishMessageToResultsQueue()
        {
            // Arrange
            var worker = new Worker(
                _mockRabbitMqOptions.Object,
                _mockFileStorageService.Object,
                _mockRabbitMqService.Object,
                _mockLogger.Object
            );

            var ocrResult = new OCRResult
            {
                Document = new DocumentDTO
                {
                    Id = 123,
                    Name = "Test Document",
                    Author = "Author",
                    FilePath = "test.pdf",
                    LastModified = DateTime.UtcNow
                },
                OcrText = "Sample OCR Text"
            };

            // Act
            await worker.SendResultAsync(ocrResult);

            // Assert
            _mockRabbitMqService.Verify(m => m.PublishMessageAsync(
                It.Is<OCRResult>(r => r.Document.Id == 123 && r.OcrText == "Sample OCR Text"),
                "ocr_results_queue"
            ), Times.Once);
        }
    }
}
