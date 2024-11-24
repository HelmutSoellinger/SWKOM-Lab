using System.Linq;
using System.Text.Json;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using DMSystem.OCRWorker;
using DMSystem.Messaging;

namespace DMSystem.Tests.OCRTests
{
    public class WorkerTests
    {
        private readonly Mock<ILogger<Worker>> _loggerMock = new Mock<ILogger<Worker>>();
        private readonly Mock<IOptions<RabbitMQSetting>> _settingsMock = new Mock<IOptions<RabbitMQSetting>>();
        private readonly Mock<IConnection> _connectionMock = new Mock<IConnection>();
        private readonly Mock<IModel> _channelMock = new Mock<IModel>();
        private readonly RabbitMQSetting _rabbitMQSetting = new RabbitMQSetting
        {
            HostName = "localhost",
            QueueName = "ocrQueue",
            UserName = "guest",
            Password = "guest"
        };

        public WorkerTests()
        {
            _settingsMock.Setup(x => x.Value).Returns(_rabbitMQSetting);
        }

        [Fact]
        public async Task StartAsync_InitializesRabbitMQ_Successfully()
        {
            // Arrange
            var worker = new Worker(_loggerMock.Object, _settingsMock.Object, _connectionMock.Object, _channelMock.Object);

            // Act
            await worker.StartAsync(default);

            // Assert
            var logMessages = _loggerMock.Invocations
                .Where(i => i.Method.Name == nameof(ILogger.Log))
                .Select(i => i.Arguments[2]?.ToString())
                .ToList();

            Assert.Contains("Starting RabbitMQ worker...", logMessages);
        }

        [Fact]
        public async Task ProcessMessage_ValidMessage_ProcessesSuccessfully()
        {
            // Arrange
            var worker = new Worker(_loggerMock.Object, _settingsMock.Object, _connectionMock.Object, _channelMock.Object);
            var testMessage = new OCRRequest { DocumentId = "123", PdfUrl = "http://example.com/test.pdf" };
            var messageJson = JsonSerializer.Serialize(testMessage);

            // Act
            await worker.ProcessMessage(messageJson);

            // Assert
            var logMessages = _loggerMock.Invocations
                .Where(i => i.Method.Name == nameof(ILogger.Log))
                .Select(i => i.Arguments[2]?.ToString())
                .ToList();

            Assert.Contains("Received message: ", logMessages);
            Assert.Contains("OCR result for DocumentId 123 sent to queue.", logMessages);
        }
    }
}