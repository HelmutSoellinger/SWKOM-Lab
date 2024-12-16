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
        private readonly string _deleteQueueName;

        public Worker(
            IOptions<RabbitMQSettings> rabbitMqOptions,
            IElasticSearchService elasticSearchService,
            IRabbitMQService rabbitMqService,
            ILogger<Worker> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _elasticSearchService = elasticSearchService ?? throw new ArgumentNullException(nameof(elasticSearchService));
            _rabbitMqService = rabbitMqService ?? throw new ArgumentNullException(nameof(rabbitMqService));

            var rabbitMqSettings = rabbitMqOptions?.Value ?? throw new ArgumentNullException(nameof(rabbitMqOptions), "RabbitMQ settings are null.");

            if (!rabbitMqSettings.Queues.TryGetValue("OcrResultsQueue", out var ocrResultsQueue) || string.IsNullOrWhiteSpace(ocrResultsQueue))
            {
                throw new Exception("The OcrResultsQueue name is missing or invalid in RabbitMQ settings.");
            }

            if (!rabbitMqSettings.Queues.TryGetValue("DeleteQueue", out var deleteQueue) || string.IsNullOrWhiteSpace(deleteQueue))
            {
                throw new Exception("The DeleteQueue name is missing or invalid in RabbitMQ settings.");
            }

            _ocrResultsQueueName = ocrResultsQueue;
            _deleteQueueName = deleteQueue;
        }


        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Elasticsearch Worker started. Listening on queues: {OcrResultsQueue}, {DeleteQueue}", _ocrResultsQueueName, _deleteQueueName);

            // Consume messages from the OCR Results Queue
            _rabbitMqService.ConsumeQueue<OCRResult>(_ocrResultsQueueName, async ocrResult =>
            {
                if (ocrResult == null)
                {
                    _logger.LogError("Received null OCRResult. Skipping processing.");
                    return;
                }

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
                    _logger.LogError(ex, "Error indexing OCR result for Document ID: {DocumentId}", docId);
                }
            });

            // Consume messages from the Delete Queue
            _rabbitMqService.ConsumeQueue<DeleteDocumentMessage>(_deleteQueueName, async deleteMessage =>
            {
                if (deleteMessage == null)
                {
                    _logger.LogError("Received null DeleteDocumentMessage. Skipping processing.");
                    return;
                }

                var docId = deleteMessage.DocumentId;
                try
                {
                    _logger.LogInformation("Deleting Document ID: {DocumentId} from Elasticsearch.", docId);

                    // Delete the document from Elasticsearch
                    await _elasticSearchService.DeleteDocumentByIdAsync(docId);

                    _logger.LogInformation("Document ID: {DocumentId} deleted successfully.", docId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting document with ID: {DocumentId} from Elasticsearch.", docId);
                }
            });

            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Elasticsearch Worker is stopping.");
            return base.StopAsync(cancellationToken);
        }
    }
}
