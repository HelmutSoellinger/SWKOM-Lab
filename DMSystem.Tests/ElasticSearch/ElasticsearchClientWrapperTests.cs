using DMSystem.ElasticSearch;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Moq;

namespace DMSystem.Tests.ElasticSearch
{
    public class TestDocument
    {
        public string Name { get; set; }
    }

    public class ElasticsearchClientWrapperTests
    {
        private readonly Mock<IElasticsearchClientWrapper> _mockWrapper;
        private readonly ElasticsearchClientWrapper _wrapper;

        public ElasticsearchClientWrapperTests()
        {
            _mockWrapper = new Mock<IElasticsearchClientWrapper>(MockBehavior.Strict);
            _wrapper = new ElasticsearchClientWrapper(_mockWrapper.Object);
        }

        [Fact]
        public async Task IndexDocumentAsync_ValidResponse_ReturnsMockableIndexResponse()
        {
            // Arrange
            var document = new TestDocument { Name = "Test" }; // Named class
            var mockResponse = new MockableIndexResponse
            {
                IsValidResponse = true,
                DebugInformation = string.Empty
            };

            _mockWrapper
                .Setup(w => w.IndexDocumentAsync(
                    It.Is<TestDocument>(d => d.Name == "Test"), // Match on the named type
                    It.IsAny<Action<IndexRequestDescriptor<TestDocument>>>() // Explicit Action type
                ))
                .ReturnsAsync(mockResponse);

            // Act
            var response = await _wrapper.IndexDocumentAsync(document, _ => { });

            // Assert
            Assert.NotNull(response);
            Assert.True(response.IsValidResponse);
            Assert.Equal(string.Empty, response.DebugInformation);
        }

        [Fact]
        public async Task IndexDocumentAsync_InvalidResponse_ReturnsMockableIndexResponse()
        {
            // Arrange
            var document = new TestDocument { Name = "Test" }; // Named class
            var mockResponse = new MockableIndexResponse
            {
                IsValidResponse = false,
                DebugInformation = "Error occurred"
            };

            _mockWrapper
                .Setup(w => w.IndexDocumentAsync(
                    It.Is<TestDocument>(d => d.Name == "Test"), // Match on the named type
                    It.IsAny<Action<IndexRequestDescriptor<TestDocument>>>() // Explicit Action type
                ))
                .ReturnsAsync(mockResponse);

            // Act
            var response = await _wrapper.IndexDocumentAsync(document, _ => { });

            // Assert
            Assert.NotNull(response);
            Assert.False(response.IsValidResponse);
            Assert.Equal("Error occurred", response.DebugInformation);
        }

        [Fact]
        public async Task SearchDocumentsAsync_ValidResponse_ReturnsSearchResult()
        {
            // Arrange
            var searchResult = new SearchResult<object>
            {
                IsValid = true,
                Hits = new List<Hit<object>>
                {
                    new Hit<object> { Source = new { Name = "Test Result" } }
                }
            };

            _mockWrapper
                .Setup(w => w.SearchDocumentsAsync<object>(It.IsAny<Action<SearchRequestDescriptor<object>>>()))
                .ReturnsAsync(searchResult);

            // Act
            var result = await _wrapper.SearchDocumentsAsync<object>(_ => { });

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.Single(result.Hits);
        }

        [Fact]
        public async Task SearchDocumentsAsync_InvalidResponse_ReturnsEmptySearchResult()
        {
            // Arrange
            var searchResult = new SearchResult<object>
            {
                IsValid = false,
                DebugInformation = "Error occurred",
                Hits = new List<Hit<object>>() // No hits
            };

            _mockWrapper
                .Setup(w => w.SearchDocumentsAsync<object>(It.IsAny<Action<SearchRequestDescriptor<object>>>()))
                .ReturnsAsync(searchResult);

            // Act
            var result = await _wrapper.SearchDocumentsAsync<object>(_ => { });

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Empty(result.Hits);
        }
    }
}
