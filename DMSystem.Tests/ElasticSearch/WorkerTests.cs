using DMSystem.Contracts;
using DMSystem.Contracts.DTOs;
using DMSystem.ElasticSearch;
using DMSystem.Tests.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DMSystem.Tests.ElasticSearch
{
    public class WorkerTests
    {
        private readonly Mock<ILogger<Worker>> _mockLogger;
        private readonly Mock<IElasticSearchService> _mockElasticSearchService;
        private readonly Mock<IRabbitMQService> _mockRabbitMqService;
        private readonly Worker _worker;

        public WorkerTests()
        {
            _mockLogger = new Mock<ILogger<Worker>>();
            _mockElasticSearchService = new Mock<IElasticSearchService>();
            _mockRabbitMqService = new Mock<IRabbitMQService>();

            var rabbitMqSettings = Options.Create(new RabbitMQSettings
            {
                Queues = new Dictionary<string, string> { { "OcrResultsQueue", "test-queue" } }
            });

            _worker = new Worker(rabbitMqSettings, _mockElasticSearchService.Object, _mockRabbitMqService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task Worker_Should_ConsumeQueue_OnStart()
        {
            // Arrange
            _mockRabbitMqService
                .Setup(m => m.ConsumeQueue<OCRResult>(
                    "test-queue",
                    It.IsAny<Func<OCRResult, Task>>()))
                .Verifiable();

            // Act
            await _worker.StartAsync(CancellationToken.None);

            // Assert
            _mockRabbitMqService.Verify(m => m.ConsumeQueue<OCRResult>("test-queue", It.IsAny<Func<OCRResult, Task>>()), Times.Once());
            _mockLogger.VerifyLog(LogLevel.Information, "Listening on queue", Times.Once());
        }

        [Fact]
        public async Task Worker_Should_LogSuccess_When_IndexingSucceeds()
        {
            // Arrange
            var ocrResult = new OCRResult
            {
                Document = new DocumentDTO { Id = 1 }
            };

            _mockRabbitMqService
                .Setup(m => m.ConsumeQueue<OCRResult>(
                    "test-queue",
                    It.IsAny<Func<OCRResult, Task>>()))
                .Callback<string, Func<OCRResult, Task>>((queue, callback) => callback(ocrResult));

            _mockElasticSearchService
                .Setup(es => es.IndexDocumentAsync(ocrResult))
                .Returns(Task.CompletedTask);

            // Act
            await _worker.StartAsync(CancellationToken.None);

            // Assert
            _mockElasticSearchService.Verify(es => es.IndexDocumentAsync(ocrResult), Times.Once());
            _mockLogger.VerifyLog(LogLevel.Information, "Indexing Document ID", Times.Once());
            _mockLogger.VerifyLog(LogLevel.Information, "Document ID: 1 indexed successfully", Times.Once());
        }

        [Fact]
        public async Task Worker_Should_LogError_When_IndexingFails()
        {
            // Arrange
            var ocrResult = new OCRResult
            {
                Document = new DocumentDTO { Id = 1 }
            };

            _mockRabbitMqService
                .Setup(m => m.ConsumeQueue<OCRResult>(
                    "test-queue",
                    It.IsAny<Func<OCRResult, Task>>()))
                .Callback<string, Func<OCRResult, Task>>((queue, callback) => callback(ocrResult));

            _mockElasticSearchService
                .Setup(es => es.IndexDocumentAsync(ocrResult))
                .ThrowsAsync(new Exception("Indexing failed"));

            // Act
            await _worker.StartAsync(CancellationToken.None);

            // Assert
            _mockElasticSearchService.Verify(es => es.IndexDocumentAsync(ocrResult), Times.Once());
            _mockLogger.VerifyLog(LogLevel.Error, "Error processing OCR result", Times.Once());
        }

        [Fact]
        public async Task Worker_Should_NotThrow_When_OCRResultIsNull()
        {
            // Arrange
            _mockRabbitMqService
                .Setup(m => m.ConsumeQueue<OCRResult>(
                    "test-queue",
                    It.IsAny<Func<OCRResult?, Task>>()))
                .Callback<string, Func<OCRResult?, Task>>((queue, callback) =>
                {
                    // Explicitly invoke the callback with null
                    callback(null).Wait();
                });

            // Act
            await _worker.StartAsync(CancellationToken.None);

            // Debug logs
            Console.WriteLine("DEBUG: Captured Logs:");
            foreach (var invocation in _mockLogger.Invocations)
            {
                Console.WriteLine(invocation);
            }

            // Assert
            _mockLogger.VerifyLog(LogLevel.Error, "Received null OCRResult. Skipping processing.", Times.Once());
        }


        [Fact]
        public void Worker_Should_Throw_When_QueueNameMissing()
        {
            // Arrange
            var rabbitMqSettings = Options.Create(new RabbitMQSettings { Queues = new Dictionary<string, string>() });

            // Act & Assert
            var exception = Assert.Throws<Exception>(() =>
                new Worker(rabbitMqSettings, _mockElasticSearchService.Object, _mockRabbitMqService.Object, _mockLogger.Object));

            Assert.Equal("The OcrResultsQueue name is missing or invalid in RabbitMQ settings.", exception.Message);
        }

        [Fact]
        public async Task Worker_Should_HandleMultipleMessages_Concurrently()
        {
            // Arrange
            var messages = new[] {
        new OCRResult { Document = new DocumentDTO { Id = 1 } },
        new OCRResult { Document = new DocumentDTO { Id = 2 } }
    };

            _mockRabbitMqService
                .Setup(m => m.ConsumeQueue<OCRResult>(
                    "test-queue",
                    It.IsAny<Func<OCRResult, Task>>()))
                .Callback<string, Func<OCRResult, Task>>((queue, callback) =>
                {
                    Parallel.ForEach(messages, async msg => await callback(msg));
                });

            // Act
            await _worker.StartAsync(CancellationToken.None);

            // Assert
            foreach (var msg in messages)
            {
                _mockElasticSearchService.Verify(es => es.IndexDocumentAsync(msg), Times.Once());
            }
        }
    }
}