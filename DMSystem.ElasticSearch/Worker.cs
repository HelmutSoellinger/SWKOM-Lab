using DMSystem.ElasticSearch;
using DMSystem.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        private readonly IModel _channel;

        public Worker(ILogger<Worker> logger, IElasticSearchService elasticSearchService)
        {
            _logger = logger;
            _elasticSearchService = elasticSearchService;

            // Set up RabbitMQ connection
            var factory = new ConnectionFactory
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
                UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest",
                Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest"
            };

            var connection = factory.CreateConnection();
            _channel = connection.CreateModel();

            // Declare the queue
            _channel.QueueDeclare("ocrResultsQueue", durable: true, exclusive: false, autoDelete: false);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
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

            _channel.BasicConsume("ocrResultsQueue", autoAck: true, consumer);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            base.Dispose();
        }
    }
}
