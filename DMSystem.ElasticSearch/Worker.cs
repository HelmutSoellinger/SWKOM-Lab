using DMSystem.Contracts;
using Microsoft.Extensions.Options;

namespace DMSystem.ElasticSearch
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IElasticSearchService _elasticSearchService;
        private readonly IRabbitMQService _rabbitMqService;
        private readonly string _ocrResultsQueueName;

        public Worker(
            IOptions<RabbitMQSettings> rabbitMqOptions,
            IElasticSearchService elasticSearchService,
            IRabbitMQService rabbitMqService,
            ILogger<Worker> logger)
        {
            _logger = logger;
            _elasticSearchService = elasticSearchService;
            _rabbitMqService = rabbitMqService;

            // Retrieve the OcrResultsQueue name from the RabbitMQ settings
            _ocrResultsQueueName = rabbitMqOptions.Value.Queues["OcrResultsQueue"];
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Elasticsearch Indexer Worker started. Listening on queue: {Queue}", _ocrResultsQueueName);

            // Consume OCR results using the centralized RabbitMQ service
            _rabbitMqService.ConsumeQueue<OCRResult>(_ocrResultsQueueName, async ocrResult =>
            {
                var docId = ocrResult.Document.Id;
                try
                {
                    _logger.LogInformation("Indexing Document ID: {DocumentId}", docId);

                    // Index the OCR result into Elasticsearch
                    await _elasticSearchService.IndexDocumentAsync(ocrResult);

                    _logger.LogInformation("Document ID: {DocumentId} indexed successfully.", docId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing OCR result for Document ID: {DocumentId}", docId);
                    // Consider retry or DLQ handling if needed
                }
            });

            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Elasticsearch Indexer Worker is stopping.");
            return base.StopAsync(cancellationToken);
        }
    }
}
