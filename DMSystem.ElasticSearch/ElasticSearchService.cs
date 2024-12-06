using Elastic.Clients.Elasticsearch;
using DMSystem.Messaging;
using System.Threading.Tasks;
using DMSystem.ElasticSearch;

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
}
