using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DMSystem.Contracts;
using DMSystem.Contracts.DTOs;
using DMSystem.ElasticSearch;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Moq;
using Xunit;

namespace DMSystem.Tests.ElasticSearchTests
{
    public class ElasticSearchServiceTests
    {
        private readonly Mock<ElasticsearchClient> _mockClient;
        private readonly ElasticSearchService _elasticSearchService;

        public ElasticSearchServiceTests()
        {
            _mockClient = new Mock<ElasticsearchClient>(MockBehavior.Strict);
            _elasticSearchService = new ElasticSearchService("http://localhost:9200");
        }

        [Fact]
        public async Task IndexDocumentAsync_ShouldIndexDocumentSuccessfully()
        {
            // Arrange
            var ocrResult = new OCRResult
            {
                Document = new DocumentDTO { Id = 1, Name = "Sample Document", Author = "John Doe" },
                OcrText = "Sample OCR text"
            };

            var indexResponse = Mock.Of<IndexResponse>(r => r.IsValidResponse == true);

            _mockClient.Setup(client => client.IndexAsync(
                It.IsAny<IndexRequest<OCRResult>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(indexResponse);

            // Act
            await _elasticSearchService.IndexDocumentAsync(ocrResult);

            // Assert
            _mockClient.Verify(client => client.IndexAsync(
                It.Is<IndexRequest<OCRResult>>(r => r.Document != null && r.Document.Document.Id == ocrResult.Document.Id),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task IndexDocumentAsync_ShouldThrowException_WhenIndexingFails()
        {
            // Arrange
            var ocrResult = new OCRResult
            {
                Document = new DocumentDTO { Id = 1, Name = "Sample Document", Author = "John Doe" },
                OcrText = "Sample OCR text"
            };

            var indexResponse = Mock.Of<IndexResponse>(r => r.IsValidResponse == false && r.DebugInformation == "Indexing failed");

            _mockClient.Setup(client => client.IndexAsync(
                It.IsAny<IndexRequest<OCRResult>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(indexResponse);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(async () =>
                await _elasticSearchService.IndexDocumentAsync(ocrResult)
            );
            Assert.Contains("Failed to index document", exception.Message);
        }

        [Fact]
        public async Task SearchDocumentsAsync_ShouldReturnResults_WhenMatchesFound()
        {
            // Arrange
            var searchTerm = "Sample";
            var expectedResults = new List<OCRResult>
            {
                new OCRResult { Document = new DocumentDTO { Id = 1, Name = "Sample Document", Author = "John Doe" }, OcrText = "Sample OCR text" },
                new OCRResult { Document = new DocumentDTO { Id = 2, Name = "Another Document", Author = "Jane Doe" }, OcrText = "Another OCR text" }
            };

            var searchResponse = Mock.Of<SearchResponse<OCRResult>>(r => r.IsValidResponse == true &&
                r.Hits == expectedResults.Select(result => Mock.Of<Hit<OCRResult>>(h => h.Source == result)).ToList());

            _mockClient.Setup(client => client.SearchAsync<OCRResult>(
                It.IsAny<SearchRequest>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResponse);

            // Act
            var results = await _elasticSearchService.SearchDocumentsAsync(searchTerm);

            // Assert
            Assert.NotNull(results);
            Assert.Equal(expectedResults.Count, results.Count());
            Assert.Equal(expectedResults.First().Document.Id, results.First().Document.Id);
        }

        [Fact]
        public async Task SearchDocumentsAsync_ShouldThrowException_WhenSearchFails()
        {
            // Arrange
            var searchTerm = "Sample";

            var searchResponse = Mock.Of<SearchResponse<OCRResult>>(r => r.IsValidResponse == false && r.DebugInformation == "Search query failed");

            _mockClient.Setup(client => client.SearchAsync<OCRResult>(
                It.IsAny<SearchRequest>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResponse);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(async () =>
                await _elasticSearchService.SearchDocumentsAsync(searchTerm)
            );
            Assert.Contains("Search query failed", exception.Message);
        }
    }
}
