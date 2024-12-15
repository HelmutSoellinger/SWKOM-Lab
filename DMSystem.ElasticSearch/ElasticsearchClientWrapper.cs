using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DMSystem.ElasticSearch
{
    public interface IElasticsearchClientWrapper
    {
        Task<IndexResponse> IndexAsync<T>(
            T document,
            IndexName index,
            CancellationToken cancellationToken = default) where T : class;

        Task<SearchResponse<T>> SearchAsync<T>(
            SearchRequest request,
            CancellationToken cancellationToken = default) where T : class;
    }

    public class ElasticsearchClientWrapper : IElasticsearchClientWrapper
    {
        private readonly ElasticsearchClient _client;

        public ElasticsearchClientWrapper(ElasticsearchClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<IndexResponse> IndexAsync<T>(
            T document,
            IndexName index,
            CancellationToken cancellationToken = default) where T : class
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document), "The document cannot be null.");
            if (index == null)
                throw new ArgumentNullException(nameof(index), "The index cannot be null.");

            var request = new IndexRequest<T>(document, index);
            return await _client.IndexAsync(request, cancellationToken);
        }

        public async Task<SearchResponse<T>> SearchAsync<T>(
            SearchRequest request,
            CancellationToken cancellationToken = default) where T : class
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request), "The search request cannot be null.");

            return await _client.SearchAsync<T>(request, cancellationToken);
        }
    }

    // Mocked implementations for testing (example usage in test context)
    public class MockElasticsearchClientWrapper : IElasticsearchClientWrapper
    {
        private readonly Dictionary<string, object> _mockData = new();

        public void AddMockIndexResponse<T>(T document, IndexName index, IndexResponse response)
        {
            var key = $"{typeof(T).FullName}-{index}";
            _mockData[key] = response;
        }

        public void AddMockSearchResponse<T>(SearchRequest request, SearchResponse<T> response) where T : class
        {
            var key = $"Search-{typeof(T).FullName}-{request.Query}";
            _mockData[key] = response;
        }

        public Task<IndexResponse> IndexAsync<T>(T document, IndexName index, CancellationToken cancellationToken = default) where T : class
        {
            var key = $"{typeof(T).FullName}-{index}";
            if (_mockData.TryGetValue(key, out var response) && response is IndexResponse indexResponse)
            {
                return Task.FromResult(indexResponse);
            }
            throw new InvalidOperationException("No mock response configured for IndexAsync.");
        }

        public Task<SearchResponse<T>> SearchAsync<T>(SearchRequest request, CancellationToken cancellationToken = default) where T : class
        {
            var key = $"Search-{typeof(T).FullName}-{request.Query}";
            if (_mockData.TryGetValue(key, out var response) && response is SearchResponse<T> searchResponse)
            {
                return Task.FromResult(searchResponse);
            }
            throw new InvalidOperationException("No mock response configured for SearchAsync.");
        }
    }
}
