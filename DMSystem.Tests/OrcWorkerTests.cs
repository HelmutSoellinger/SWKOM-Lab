using Xunit;
using Moq;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using DMSystem.OCRWorker;
using DMSystem.Messaging;
using DMSystem.Minio;

namespace DMSystem.Tests
{
    public class WorkerTests
    {
        private Mock<IOptions<RabbitMQSetting>> _mockRabbitMqOptions;
        private Mock<MinioFileStorageService> _mockFileStorageService;
        private Mock<ILogger<Worker>> _mockLogger;
        private Mock<IConnection> _mockConnection;
        private Mock<IModel> _mockChannel;

        public WorkerTests()
        {
            _mockRabbitMqOptions = new Mock<IOptions<RabbitMQSetting>>();
            _mockFileStorageService = new Mock<MinioFileStorageService>();
            _mockLogger = new Mock<ILogger<Worker>>();
            _mockConnection = new Mock<IConnection>();
            _mockChannel = new Mock<IModel>();
        }

        private void SetupConsumer()
        {
            var consumer = new EventingBasicConsumer(_mockChannel.Object);
            consumer.Received += (sender, e) =>
            {
                var body = e.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                // Process the message here
            };
            _mockChannel.Setup(c => c.BasicConsume(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), consumer));
        }
        /*
        [Fact]
        public async Task ExecuteAsync_ShouldProcessOCRRequests()
        {
            // Arrange
            var worker = new Worker(_mockRabbitMqOptions.Object, _mockFileStorageService.Object, _mockLogger.Object);

            SetupConsumer();

            var mockBody = Encoding.UTF8.GetBytes("{\"DocumentId\":\"test\",\"PdfUrl\":\"http://example.com/test.pdf\"}");
            _mockChannel.Setup(c => c.BasicGet(It.IsAny<string>(), It.IsAny<bool>()))
                         .Returns(new BasicGetResult(_mockChannel.Object, "", "", false, "", "", "", "", "", "", "", "", "", mockBody));

            // Act
            await worker.ExecuteAsync(new CancellationToken());

            // Assert
            _mockFileStorageService.Verify(f => f.DownloadFileAsync("test.pdf"), Times.Once);
        }
        */
        [Fact]
        public async Task PerformOcrAsync_ShouldExtractTextFromPdf()
        {
            // Arrange
            var worker = new Worker(_mockRabbitMqOptions.Object, _mockFileStorageService.Object, _mockLogger.Object);

            var mockFileStream = new MemoryStream(Encoding.UTF8.GetBytes("Test PDF Content"));
            _mockFileStorageService.Setup(f => f.DownloadFileAsync("test.pdf")).ReturnsAsync(mockFileStream);

            // Act
            var result = await worker.PerformOcrAsync("test.pdf");

            // Assert
            Assert.NotEmpty(result);
        }

      /*  [Fact]
        public void SendResult_ShouldPublishToResultsQueue()
        {
            // Arrange
            var worker = new Worker(_mockRabbitMqOptions.Object, _mockFileStorageService.Object, _mockLogger.Object);

            var mockResult = new OCRResult { DocumentId = "test", OcrText = "Test Result" };
            var expectedMessage = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(mockResult));

            // Act
            worker.SendResult(mockResult);

            // Assert
            _mockChannel.Verify(c => c.BasicPublish(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                It.IsAny<byte[]>()), Times.Once);
        }
      

        [Fact]
        public async Task ExecuteAsync_ShouldHandleInvalidMessages()
        {
            // Arrange
            var worker = new Worker(_mockRabbitMqOptions.Object, _mockFileStorageService.Object, _mockLogger.Object);

            var mockBody = Encoding.UTF8.GetBytes("Invalid JSON");
            _mockChannel.Setup(c => c.BasicGet(It.IsAny<string>(), It.IsAny<bool>()))
                         .Returns(new BasicGetResult(_mockChannel.Object, "", "", false, "", "", "", "", "", "", "", "", "", mockBody));

            // Act & Assert
            await worker.ExecuteAsync(new CancellationToken());
            _mockLogger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }
      */
        [Fact]
        public async Task PerformOcrAsync_ShouldThrowExceptionOnFailure()
        {
            // Arrange
            var worker = new Worker(_mockRabbitMqOptions.Object, _mockFileStorageService.Object, _mockLogger.Object);

            _mockFileStorageService.Setup(f => f.DownloadFileAsync("test.pdf")).Throws<Exception>();

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => worker.PerformOcrAsync("test.pdf"));
        }
    }
}
