using DMSystem.Contracts;
using Elastic.Clients.Elasticsearch;

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
                .Id(ocrResult.Document.Id.ToString())
            );

            if (!response.IsValidResponse)
            {
                throw new Exception($"Failed to index document: {response.DebugInformation}");
            }
        }

        public async Task<IEnumerable<OCRResult>> SearchDocumentsAsync(string searchTerm)
        {
            var searchResponse = await _client.SearchAsync<OCRResult>(s => s
                .Index("ocr-results")
                .Query(q => q
                    .MultiMatch(m => m
                        .Query(searchTerm)
                        .Fields(new[] { "Document.Name", "Document.Author", "OcrText" })
                    )
                )
            );

            if (!searchResponse.IsValidResponse)
            {
                throw new Exception($"Search query failed: {searchResponse.DebugInformation}");
            }

            // Return the full OCRResult from _source for each hit.
            // This means each result includes Document (with Id, Name, Author, etc.) and OcrText.
            return searchResponse.Hits
                .Where(hit => hit.Source is not null)
                .Select(hit => hit.Source!);
        }
    }
}
