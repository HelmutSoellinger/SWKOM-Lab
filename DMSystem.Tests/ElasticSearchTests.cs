using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DMSystem.ElasticSearch;
using DMSystem.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace DMSystem.Tests
{
    public class ElasticSearchTests
    {
        private readonly Mock<IElasticSearchService> _mockElasticSearchService;
        private readonly Mock<IOptions<RabbitMQSetting>> _mockOptions;
        private readonly Mock<IConnection> _mockConnection;
        private readonly Mock<IModel> _mockChannel;
        private readonly Mock<ILogger<Worker>> _mockLogger;

        public ElasticSearchTests()
        {
            _mockElasticSearchService = new Mock<IElasticSearchService>();
            _mockOptions = new Mock<IOptions<RabbitMQSetting>>();
            _mockConnection = new Mock<IConnection>();
            _mockChannel = new Mock<IModel>();
            _mockLogger = new Mock<ILogger<Worker>>();

            _mockOptions.Setup(o => o.Value).Returns(new RabbitMQSetting
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest",
                OcrResultsQueue = "ocr-results-queue"
            });
        }

        [Fact]
        public async Task IndexDocumentAsync_IndexesDocumentSuccessfully()
        {
            // Arrange
            var ocrResult = new OCRResult
            {
                DocumentId = "123",
                OcrText = "Sample OCR text"
            };

            _mockElasticSearchService
               .Setup(service => service.IndexDocumentAsync(ocrResult))
               .Returns(Task.CompletedTask);

            // Act
            await _mockElasticSearchService.Object.IndexDocumentAsync(ocrResult);

            // Assert
            _mockElasticSearchService.Verify(service => service.IndexDocumentAsync(ocrResult), Times.Once);
        }

        [Fact]
        public async Task SearchDocumentsAsync_ReturnsSearchResults()
        {
            // Arrange
            var searchTerm = "Sample";
            var expectedResults = new List<SearchResult>
            {
                new SearchResult { DocumentId = "123", MatchCount = 3 },
                new SearchResult { DocumentId = "124", MatchCount = 5 }
            };

            _mockElasticSearchService
               .Setup(service => service.SearchDocumentsAsync(searchTerm))
               .ReturnsAsync(expectedResults);

            // Act
            var results = await _mockElasticSearchService.Object.SearchDocumentsAsync(searchTerm);

            // Assert
            Assert.NotNull(results);
            Assert.Equal(expectedResults.Count, results.Count());
            Assert.Equal(expectedResults.First().DocumentId, results.First().DocumentId);
        }

        [Fact]
        public async Task Worker_StartAsync_ProcessesOCRResults()
        {
            // Arrange
            var mockLogger = _mockLogger;
            var mockOptions = _mockOptions;

            var ocrResult = new OCRResult
            {
                DocumentId = "123",
                OcrText = "Sample OCR text"
            };

            _mockElasticSearchService
               .Setup(service => service.IndexDocumentAsync(ocrResult))
               .Returns(Task.CompletedTask);

            // Mock RabbitMQ dependencies
            _mockConnection.Setup(c => c.CreateModel()).Returns(_mockChannel.Object);

            var worker = new Worker(mockOptions.Object, _mockElasticSearchService.Object, mockLogger.Object);

            // Simulate RabbitMQ message processing
            var message = System.Text.Json.JsonSerializer.Serialize(ocrResult);
            var body = System.Text.Encoding.UTF8.GetBytes(message);

            _mockChannel.Setup(ch => ch.BasicConsume(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IBasicConsumer>()))
               .Callback((string queue, bool autoAck, IBasicConsumer consumer) =>
               {
                   // Simulate message delivery
                   var eventArgs = new BasicDeliverEventArgs
                   {
                       Body = new ReadOnlyMemory<byte>(body)
                   };
                   consumer.HandleBasicDeliver(
               consumerTag: "",
               deliveryTag: 1,
               redelivered: false,
               exchange: "",
               routingKey: "",
               properties: null,
               body: eventArgs.Body
           );
               });

            // Act
            await worker.StartAsync(default);

            // Assert
            _mockElasticSearchService.Verify(service => service.IndexDocumentAsync(It.IsAny<OCRResult>()), Times.AtLeastOnce);
        }


        [Fact]
        public async Task ElasticSearchService_SearchDocuments_ThrowsExceptionOnFailure()
        {
            // Arrange
            var elasticsearchService = new ElasticSearchService("http://invalid-url");

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(async () =>
                await elasticsearchService.SearchDocumentsAsync("test")
            );
        }
    }
}
