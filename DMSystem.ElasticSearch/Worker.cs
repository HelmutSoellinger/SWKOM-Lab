using DMSystem.ElasticSearch;
using DMSystem.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace DMSystem.ElasticSearch
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IElasticSearchService _elasticSearchService;
        private readonly RabbitMQSetting _rabbitMqSettings;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public Worker(
            IOptions<RabbitMQSetting> rabbitMqOptions,
            IElasticSearchService elasticSearchService,
            ILogger<Worker> logger)
        {
            _logger = logger;
            _elasticSearchService = elasticSearchService;
            _rabbitMqSettings = rabbitMqOptions.Value;

            // Initialize RabbitMQ connection and channel
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqSettings.HostName,
                UserName = _rabbitMqSettings.UserName,
                Password = _rabbitMqSettings.Password
            };

            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declare the OCR results queue
                _channel.QueueDeclare(
                    queue: _rabbitMqSettings.OcrResultsQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                _logger.LogInformation($"Connected to RabbitMQ at {_rabbitMqSettings.HostName}. Queue declared: {_rabbitMqSettings.OcrResultsQueue}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                throw;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Elasticsearch Indexer Worker started.");

            // Set up RabbitMQ consumer for OCR results
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    // Deserialize the OCR result
                    var ocrResult = JsonSerializer.Deserialize<OCRResult>(message);

                    if (ocrResult != null)
                    {
                        _logger.LogInformation($"Indexing Document ID: {ocrResult.DocumentId}");

                        // Index OCR result into Elasticsearch
                        await _elasticSearchService.IndexDocumentAsync(ocrResult);

                        _logger.LogInformation($"Document ID: {ocrResult.DocumentId} indexed successfully.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing OCR result.");
                }
            };

            // Consume messages from the results queue
            _channel.BasicConsume(
                queue: _rabbitMqSettings.OcrResultsQueue,
                autoAck: true,
                consumer: consumer);

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Elasticsearch Indexer Worker is stopping.");

            if (_channel.IsOpen)
            {
                _channel.Close();
                _connection.Close();
            }

            return base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}
