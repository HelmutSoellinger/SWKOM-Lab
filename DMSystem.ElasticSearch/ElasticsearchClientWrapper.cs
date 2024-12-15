using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;

namespace DMSystem.ElasticSearch
{
    public interface IElasticsearchClientWrapper
    {
        Task<MockableIndexResponse> IndexDocumentAsync<T>(T document, Action<IndexRequestDescriptor<T>> configureRequest)
            where T : class;

        Task<SearchResult<T>> SearchDocumentsAsync<T>(Action<SearchRequestDescriptor<T>> configureRequest)
            where T : class;
    }

    public class SearchResult<T>
    {
        public bool IsValid { get; set; } // Equivalent to IsValidResponse
        public string DebugInformation { get; set; } = string.Empty; // Store debug information
        public IReadOnlyCollection<Hit<T>> Hits { get; set; } = new List<Hit<T>>();
    }

    public class MockableIndexResponse
    {
        public bool IsValidResponse { get; set; }
        public string DebugInformation { get; set; } = string.Empty;
    }

    public class ElasticsearchClientWrapper : IElasticsearchClientWrapper
    {
        private readonly ElasticsearchClient _client;

        // Wrapper for testing
        private readonly IElasticsearchClientWrapper _clientWrapper;

        // Constructor for testing with IElasticsearchClientWrapper
        public ElasticsearchClientWrapper(IElasticsearchClientWrapper clientWrapper)
        {
            _clientWrapper = clientWrapper ?? throw new ArgumentNullException(nameof(clientWrapper));
        }

        // Constructor for production
        public ElasticsearchClientWrapper(string elasticsearchUrl)
        {
            if (string.IsNullOrWhiteSpace(elasticsearchUrl))
                throw new ArgumentException("Elasticsearch URL cannot be null or empty.", nameof(elasticsearchUrl));

            var settings = new ElasticsearchClientSettings(new Uri(elasticsearchUrl));
            _client = new ElasticsearchClient(settings);
        }

        // Constructor for testing with ElasticsearchClient
        public ElasticsearchClientWrapper(ElasticsearchClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client), "Elasticsearch client cannot be null.");
        }

        public async Task<MockableIndexResponse> IndexDocumentAsync<T>(T document, Action<IndexRequestDescriptor<T>> configureRequest)
            where T : class
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document), "Document cannot be null.");

            if (configureRequest == null)
                throw new ArgumentNullException(nameof(configureRequest), "ConfigureRequest action cannot be null.");

            // Use the test wrapper if it's initialized
            if (_clientWrapper != null)
                return await _clientWrapper.IndexDocumentAsync(document, configureRequest);

            // Otherwise, use the production client
            var response = await _client.IndexAsync(document, configureRequest);

            return new MockableIndexResponse
            {
                IsValidResponse = response.IsValidResponse,
                DebugInformation = response.DebugInformation
            };
        }

        public async Task<SearchResult<T>> SearchDocumentsAsync<T>(Action<SearchRequestDescriptor<T>> configureRequest)
            where T : class
        {
            if (configureRequest == null)
                throw new ArgumentNullException(nameof(configureRequest), "ConfigureRequest action cannot be null.");

            // Use the test wrapper if it's initialized
            if (_clientWrapper != null)
                return await _clientWrapper.SearchDocumentsAsync(configureRequest);

            // Otherwise, use the production client
            var response = await _client.SearchAsync(configureRequest);

            return new SearchResult<T>
            {
                IsValid = response.IsValidResponse,
                DebugInformation = response.DebugInformation,
                Hits = response.Hits ?? new List<Hit<T>>() // Default to an empty list if Hits is null
            };
        }
    }
}