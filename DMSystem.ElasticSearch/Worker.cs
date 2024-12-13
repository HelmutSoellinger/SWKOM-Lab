using DMSystem.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
                try
                {
                    _logger.LogInformation("Indexing Document ID: {DocumentId}", ocrResult.DocumentId);

                    // Index the OCR result into Elasticsearch
                    await _elasticSearchService.IndexDocumentAsync(ocrResult);

                    _logger.LogInformation("Document ID: {DocumentId} indexed successfully.", ocrResult.DocumentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing OCR result for Document ID: {DocumentId}", ocrResult.DocumentId);
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
