using DMSystem.Contracts;
using DMSystem.Contracts.DTOs;
using DMSystem.ElasticSearch;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Microsoft.Extensions.Logging;
using Moq;

namespace DMSystem.Tests.ElasticSearch
{
    public class ElasticSearchServiceTests
    {
        private readonly Mock<IElasticsearchClientWrapper> _mockClientWrapper;
        private readonly Mock<ILogger<ElasticSearchService>> _mockLogger;
        private readonly ElasticSearchService _elasticSearchService;

        public ElasticSearchServiceTests()
        {
            _mockClientWrapper = new Mock<IElasticsearchClientWrapper>(MockBehavior.Strict);
            _mockLogger = new Mock<ILogger<ElasticSearchService>>();
            _elasticSearchService = new ElasticSearchService(_mockClientWrapper.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task IndexDocumentAsync_ValidResponse_LogsSuccess()
        {
            // Arrange
            var ocrResult = new OCRResult
            {
                Document = new DocumentDTO { Id = 1, Name = "Test Document", Author = "Author" },
                OcrText = "Sample OCR text"
            };

            var validResponse = new MockableIndexResponse
            {
                IsValidResponse = true,
                DebugInformation = string.Empty
            };

            _mockClientWrapper
                .Setup(w => w.IndexDocumentAsync(
                    ocrResult,
                    It.IsAny<Action<IndexRequestDescriptor<OCRResult>>>()
                ))
                .ReturnsAsync(validResponse);

            // Act
            await _elasticSearchService.IndexDocumentAsync(ocrResult);

            // Assert
            _mockLogger.Verify(
                logger => logger.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(), // Generic match for log state
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task IndexDocumentAsync_InvalidResponse_ThrowsException()
        {
            // Arrange
            var ocrResult = new OCRResult
            {
                Document = new DocumentDTO { Id = 1, Name = "Test Document", Author = "Author" },
                OcrText = "Sample OCR text"
            };

            var invalidResponse = new MockableIndexResponse
            {
                IsValidResponse = false,
                DebugInformation = "Error indexing document"
            };

            _mockClientWrapper
                .Setup(w => w.IndexDocumentAsync(
                    ocrResult,
                    It.IsAny<Action<IndexRequestDescriptor<OCRResult>>>()
                ))
                .ReturnsAsync(invalidResponse);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => _elasticSearchService.IndexDocumentAsync(ocrResult));
            Assert.Contains("Error indexing document", exception.Message);

            _mockLogger.Verify(
                logger => logger.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(), // Match any log message
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SearchDocumentsAsync_ValidResponse_ReturnsResults()
        {
            // Arrange
            var document = new DocumentDTO { Id = 1, Name = "Test Document", Author = "Author" };
            var ocrResult = new OCRResult { Document = document, OcrText = "Sample OCR text" };

            var searchResult = new SearchResult<OCRResult>
            {
                IsValid = true,
                Hits = new List<Hit<OCRResult>>
        {
            new Hit<OCRResult> { Source = ocrResult }
        }
            };

            _mockClientWrapper
                .Setup(w => w.SearchDocumentsAsync<OCRResult>(
                    It.IsAny<Action<SearchRequestDescriptor<OCRResult>>>()
                ))
                .ReturnsAsync(searchResult);

            // Act
            var results = await _elasticSearchService.SearchDocumentsAsync("test");

            // Assert
            Assert.Single(results);

            // Verify all informational logs
            _mockLogger.Verify(
                logger => logger.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(2)); // Match the number of informational logs
        }

        [Fact]
        public async Task SearchDocumentsAsync_NoResults_FallbackToFuzzySearch()
        {
            // Arrange
            var emptyResult = new SearchResult<OCRResult>
            {
                IsValid = true,
                Hits = new List<Hit<OCRResult>>() // No results
            };

            var fuzzyResult = new SearchResult<OCRResult>
            {
                IsValid = true,
                Hits = new List<Hit<OCRResult>>
        {
            new Hit<OCRResult>
            {
                Source = new OCRResult
                {
                    Document = new DocumentDTO { Id = 1, Name = "Test Document", Author = "Author" },
                    OcrText = "Sample OCR text"
                }
            }
        }
            };

            _mockClientWrapper
                .SetupSequence(w => w.SearchDocumentsAsync<OCRResult>(
                    It.IsAny<Action<SearchRequestDescriptor<OCRResult>>>()
                ))
                .ReturnsAsync(emptyResult) // Standard search returns empty
                .ReturnsAsync(fuzzyResult); // Fuzzy search returns results

            // Act
            var results = await _elasticSearchService.SearchDocumentsAsync("test");

            // Assert
            Assert.Single(results);

            // Verify the specific "No results, falling back" log message
            _mockLogger.Verify(
                logger => logger.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(5)); // Verify all informational logs
        }

        [Fact]
        public async Task SearchDocumentsAsync_ExceptionThrown_LogsError()
        {
            // Arrange
            _mockClientWrapper
                .Setup(w => w.SearchDocumentsAsync<OCRResult>(
                    It.IsAny<Action<SearchRequestDescriptor<OCRResult>>>()
                ))
                .ThrowsAsync(new Exception("Search failed"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => _elasticSearchService.SearchDocumentsAsync("test"));
            Assert.Contains("Search failed", exception.Message);

            // Verify the logger was called twice with LogLevel.Error
            _mockLogger.Verify(
                logger => logger.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task FuzzySearchDocuments_InvalidResponse_ThrowsException()
        {
            // Arrange
            var invalidResult = new SearchResult<OCRResult>
            {
                IsValid = false,
                DebugInformation = "Fuzzy search failed"
            };

            _mockClientWrapper
                .Setup(w => w.SearchDocumentsAsync<OCRResult>(
                    It.IsAny<Action<SearchRequestDescriptor<OCRResult>>>()
                ))
                .ReturnsAsync(invalidResult);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => _elasticSearchService.FuzzySearchDocuments("test"));
            Assert.Contains("Fuzzy search failed", exception.Message);

            // Verify the logger invocation for LogLevel.Error
            _mockLogger.Verify(
                logger => logger.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

    }
}
