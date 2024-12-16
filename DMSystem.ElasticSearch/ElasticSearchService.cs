using Microsoft.Extensions.Logging;
using DMSystem.Contracts;
using Elastic.Clients.Elasticsearch;

namespace DMSystem.ElasticSearch
{
    public class ElasticSearchService : IElasticSearchService
    {
        private readonly IElasticsearchClientWrapper _clientWrapper; // Use the wrapper
        private readonly ILogger<ElasticSearchService> _logger; // Injected logger

        public ElasticSearchService(IElasticsearchClientWrapper clientWrapper, ILogger<ElasticSearchService> logger)
        {
            _clientWrapper = clientWrapper ?? throw new ArgumentNullException(nameof(clientWrapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task IndexDocumentAsync(OCRResult ocrResult)
        {
            var response = await _clientWrapper.IndexDocumentAsync(ocrResult, i => i
                .Index("ocr-results")
                .Id(ocrResult.Document.Id.ToString())
            );

            if (!response.IsValidResponse)
            {
                _logger.LogError("Failed to index document: {DebugInformation}", response.DebugInformation);
                throw new Exception($"Failed to index document: {response.DebugInformation}");
            }

            _logger.LogInformation("Document indexed successfully: {DocumentId}", ocrResult.Document.Id);
        }

        public async Task<IEnumerable<OCRResult>> SearchDocumentsAsync(string searchTerm)
        {
            try
            {
                // Perform standard search
                var results = await SearchDocuments(searchTerm);

                if (results.Any())
                {
                    return results;
                }

                // If no results, fallback to fuzzy search
                _logger.LogInformation("No results found for term: {SearchTerm}. Falling back to fuzzy search.", searchTerm);
                return await FuzzySearchDocuments(searchTerm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during search process for term: {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<IEnumerable<OCRResult>> SearchDocuments(string searchTerm)
        {
            try
            {
                _logger.LogInformation("Executing standard Elasticsearch query for term: {SearchTerm}", searchTerm);

                var searchResponse = await _clientWrapper.SearchDocumentsAsync<OCRResult>(s => s
                    .Index("ocr-results")
                    .Query(q => q
                        .Bool(b => b
                            .Should(
                                bs => bs.Match(m => m
                                    .Field(f => f.Document.Name)
                                    .Query(searchTerm)
                                ),
                                bs => bs.Match(m => m
                                    .Field(f => f.Document.Author)
                                    .Query(searchTerm)
                                ),
                                bs => bs.Match(m => m
                                    .Field(f => f.OcrText)
                                    .Query(searchTerm)
                                ),
                                bs => bs.Wildcard(w => w
                                    .Field(f => f.Document.Name)
                                    .Value($"*{searchTerm}*")
                                ),
                                bs => bs.Wildcard(w => w
                                    .Field(f => f.Document.Author)
                                    .Value($"*{searchTerm}*")
                                ),
                                bs => bs.Wildcard(w => w
                                    .Field(f => f.OcrText)
                                    .Value($"*{searchTerm}*")
                                )
                            )
                        )
                    )
                );

                if (!searchResponse.IsValid)
                {
                    _logger.LogError("Standard search failed: {DebugInformation}", searchResponse.DebugInformation);
                    throw new Exception($"Search query failed: {searchResponse.DebugInformation}");
                }

                _logger.LogInformation("Standard search succeeded with {HitsCount} hits.", searchResponse.Hits.Count);

                return searchResponse.Hits
                    .Where(hit => hit.Source != null)
                    .Select(hit => hit.Source!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing standard search for term: {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<IEnumerable<OCRResult>> FuzzySearchDocuments(string searchTerm)
        {
            try
            {
                _logger.LogInformation("Executing fuzzy Elasticsearch query for term: {SearchTerm}", searchTerm);

                var fuzzyResponse = await _clientWrapper.SearchDocumentsAsync<OCRResult>(s => s
                    .Index("ocr-results")
                    .Query(q => q
                        .Bool(b => b
                            .Should(
                                bs => bs.Fuzzy(f => f
                                    .Field(f => f.Document.Name)
                                    .Value(searchTerm)
                                    .Fuzziness(new Fuzziness(2))
                                ),
                                bs => bs.Fuzzy(f => f
                                    .Field(f => f.Document.Author)
                                    .Value(searchTerm)
                                    .Fuzziness(new Fuzziness(2))
                                ),
                                bs => bs.Fuzzy(f => f
                                    .Field(f => f.OcrText)
                                    .Value(searchTerm)
                                    .Fuzziness(new Fuzziness(2))
                                )
                            )
                        )
                    )
                );

                if (!fuzzyResponse.IsValid)
                {
                    _logger.LogError("Fuzzy search failed: {DebugInformation}", fuzzyResponse.DebugInformation);
                    throw new Exception($"Fuzzy search query failed: {fuzzyResponse.DebugInformation}");
                }

                _logger.LogInformation("Fuzzy search succeeded with {HitsCount} hits.", fuzzyResponse.Hits.Count);

                return fuzzyResponse.Hits
                    .Where(hit => hit.Source != null)
                    .Select(hit => hit.Source!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing fuzzy search for term: {SearchTerm}", searchTerm);
                throw;
            }
        }

        // New Method: Delete Document by ID
        public async Task DeleteDocumentByIdAsync(int documentId)
        {
            try
            {
                _logger.LogInformation("Deleting document with ID: {DocumentId} from Elasticsearch.", documentId);

                var response = await _clientWrapper.DeleteDocumentAsync("ocr-results", documentId.ToString());

                if (!response.IsValidResponse)
                {
                    _logger.LogError("Failed to delete document with ID {DocumentId}: {DebugInformation}", documentId, response.DebugInformation);
                    throw new Exception($"Failed to delete document with ID {documentId}: {response.DebugInformation}");
                }

                _logger.LogInformation("Document with ID {DocumentId} deleted successfully.", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document with ID: {DocumentId}", documentId);
                throw;
            }
        }
    }
}
