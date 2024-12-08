using Elastic.Clients.Elasticsearch;
using DMSystem.Messaging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DMSystem.ElasticSearch;

namespace DMSystem.ElasticSearch
{
    public class ElasticSearchService : IElasticSearchService
    {
        private readonly ElasticsearchClient _client;

        public ElasticSearchService(string elasticsearchUrl)
        {
            var settings = new ElasticsearchClientSettings(new Uri(elasticsearchUrl));
            _client = new ElasticsearchClient(settings);
        }

        public async Task IndexDocumentAsync(OCRResult ocrResult)
        {
            var response = await _client.IndexAsync(ocrResult, i => i
                .Index("ocr-results")
                .Id(ocrResult.DocumentId)
            );

            if (!response.IsValidResponse)
            {
                throw new Exception($"Failed to index document: {response.DebugInformation}");
            }
        }

        public async Task<IEnumerable<SearchResult>> SearchDocumentsAsync(string searchTerm)
        {
            var searchResponse = await _client.SearchAsync<OCRResult>(s => s
                .Index("ocr-results")
                .Query(q => q
                    .Match(m => m
                        .Field(f => f.OcrText)
                        .Query(searchTerm)
                    )
                )
            );

            if (!searchResponse.IsValidResponse)
            {
                throw new Exception($"Search query failed: {searchResponse.DebugInformation}");
            }

            // Map search results to SearchResult model
            return searchResponse.Hits.Select(hit => new SearchResult
            {
                DocumentId = hit.Id,
                MatchCount = hit.Highlight["ocrText"].Count
            });
        }
    }
}