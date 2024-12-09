using Xunit;
using Moq;
using System.IO;
using System.Text;
using System.Text.Json; // Added namespace for JsonSerializer
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using DMSystem.Messaging;
using DMSystem.Minio;
using DMSystem.OCRWorker;

namespace DMSystem.Tests
{
    public class WorkerTests
    {
        private readonly Mock<IOptions<RabbitMQSetting>> _mockRabbitMqOptions;
        private readonly Mock<MinioFileStorageService> _mockFileStorageService;
        private readonly Mock<ILogger<Worker>> _mockLogger;
        private readonly Mock<IConnection> _mockConnection;
        private readonly Mock<IModel> _mockChannel;

        public WorkerTests()
        {
            _mockRabbitMqOptions = new Mock<IOptions<RabbitMQSetting>>();
            _mockFileStorageService = new Mock<MinioFileStorageService>();
            _mockLogger = new Mock<ILogger<Worker>>();
            _mockConnection = new Mock<IConnection>();
            _mockChannel = new Mock<IModel>();

            // Set up RabbitMQ settings
            var rabbitMqSettings = new RabbitMQSetting
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest",
                OcrQueue = "ocr_queue",
                OcrResultsQueue = "ocr_results_queue"
            };
            _mockRabbitMqOptions.Setup(o => o.Value).Returns(rabbitMqSettings);

            // Mock RabbitMQ connection and channel behavior
            _mockConnection.Setup(c => c.CreateModel()).Returns(_mockChannel.Object);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldProcessOCRRequests()
        {
            // Arrange
            var worker = new Worker(_mockRabbitMqOptions.Object, _mockFileStorageService.Object, _mockLogger.Object);

            var mockMessage = new OCRRequest { DocumentId = "123", PdfUrl = "test.pdf" };
            var messageBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(mockMessage));

            var consumer = new EventingBasicConsumer(_mockChannel.Object);
            _mockChannel.Setup(c => c.BasicConsume(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<EventingBasicConsumer>()))
                .Callback<string, bool, IBasicConsumer>((queue, autoAck, consumer) =>
                {
                    // Simulate a message being received
                    consumer.HandleBasicDeliver(
                        consumerTag: "test_consumer",
                        deliveryTag: 1,
                        redelivered: false,
                        exchange: "",
                        routingKey: "ocr_queue",
                        properties: null,
                        body: messageBody);
                });

            // Act
            var cts = new CancellationTokenSource();
            cts.CancelAfter(1000); // Ensure the worker stops after some time
            await worker.StartAsync(cts.Token);

            // Assert
            _mockFileStorageService.Verify(f => f.DownloadFileAsync("test.pdf"), Times.Once);
        }

        [Fact]
        public async Task PerformOcrAsync_ShouldExtractTextFromPdf()
        {
            // Arrange
            var worker = new Worker(_mockRabbitMqOptions.Object, _mockFileStorageService.Object, _mockLogger.Object);

            // Mock a file stream as the downloaded PDF content
            var mockFileStream = new MemoryStream(Encoding.UTF8.GetBytes("Test PDF Content"));
            _mockFileStorageService.Setup(f => f.DownloadFileAsync(It.IsAny<string>()))
                .ReturnsAsync(mockFileStream);

            // Act
            var result = await worker.PerformOcrAsync("test.pdf");

            // Assert
            Assert.NotNull(result);
            _mockFileStorageService.Verify(f => f.DownloadFileAsync("test.pdf"), Times.Once);
        }

        [Fact]
        public void SendResult_ShouldPublishMessageToQueue()
        {
            // Arrange
            var worker = new Worker(_mockRabbitMqOptions.Object, _mockFileStorageService.Object, _mockLogger.Object);

            var ocrResult = new OCRResult
            {
                DocumentId = "123",
                OcrText = "Sample OCR Text"
            };

            // Act
            worker.SendResult(ocrResult); // Ensure SendResult is public

            // Assert
            _mockChannel.Verify(c => c.BasicPublish(
                It.IsAny<string>(), // Removed named argument
                It.IsAny<string>(), // Removed named argument
                null,
                It.IsAny<byte[]>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldHandleInvalidMessages()
        {
            // Arrange
            var worker = new Worker(_mockRabbitMqOptions.Object, _mockFileStorageService.Object, _mockLogger.Object);

            var invalidMessage = "Invalid JSON";
            var messageBody = Encoding.UTF8.GetBytes(invalidMessage);

            var consumer = new EventingBasicConsumer(_mockChannel.Object);
            _mockChannel.Setup(c => c.BasicConsume(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<EventingBasicConsumer>()))
                .Callback<string, bool, IBasicConsumer>((queue, autoAck, consumer) =>
                {
                    consumer.HandleBasicDeliver(
                        consumerTag: "test_consumer",
                        deliveryTag: 1,
                        redelivered: false,
                        exchange: "",
                        routingKey: "ocr_queue",
                        properties: null,
                        body: messageBody);
                });

            // Act
            var cts = new CancellationTokenSource();
            cts.CancelAfter(1000);
            await worker.StartAsync(cts.Token);

            // Assert
            _mockLogger.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<object>(),
                It.IsAny<Exception>(),
                (Func<object, Exception, string>)It.IsAny<object>()), Times.Once); // Fixed lambda
        }
    }
}
