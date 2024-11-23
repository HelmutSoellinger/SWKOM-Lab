using DMSystem.Messaging;
using DMSystem.OCR;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DMSystem.OCRWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly RabbitMQSetting _rabbitMQSetting;
        private IConnection _connection;
        private IModel _channel;
        private readonly OcrProcessor _ocrProcessor;

        public Worker(ILogger<Worker> logger, IOptions<RabbitMQSetting> rabbitMQSetting)
        {
            _logger = logger;
            _rabbitMQSetting = rabbitMQSetting.Value; // Load RabbitMQ settings from DI
            _ocrProcessor = new OcrProcessor(); // Initialize OCR Processor
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting RabbitMQ worker...");
            InitializeRabbitMQ();
            return Task.CompletedTask;
        }

        private void InitializeRabbitMQ()
        {
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMQSetting.HostName,
                UserName = _rabbitMQSetting.UserName,
                Password = _rabbitMQSetting.Password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(
                queue: _rabbitMQSetting.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            _logger.LogInformation("RabbitMQ initialized successfully.");
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation($"Received message: {message}");

                try
                {
                    // Deserialize message for processing
                    var request = JsonSerializer.Deserialize<OcrRequest>(message);

                    if (request != null && !string.IsNullOrEmpty(request.PdfUrl))
                    {
                        // Fetch PDF content from the provided URL
                        var pdfContent = await FetchPdfContent(request.PdfUrl);

                        // Perform OCR
                        var ocrResult = _ocrProcessor.PerformOcr(pdfContent);

                        // Prepare and send the result back
                        var resultMessage = new OcrResult
                        {
                            DocumentId = request.DocumentId,
                            OcrText = ocrResult
                        };

                        SendOcrResult(resultMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing message: {ex.Message}");
                }
            };

            _channel.BasicConsume(
                queue: _rabbitMQSetting.QueueName,
                autoAck: true,
                consumer: consumer
            );

            return Task.CompletedTask;
        }

        private async Task<byte[]> FetchPdfContent(string pdfUrl)
        {
            using var httpClient = new HttpClient();
            _logger.LogInformation($"Fetching PDF from {pdfUrl}");
            var response = await httpClient.GetAsync(pdfUrl);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        private void SendOcrResult(OcrResult result)
        {
            var resultJson = JsonSerializer.Serialize(result);
            var resultBytes = Encoding.UTF8.GetBytes(resultJson);

            _channel.BasicPublish(
                exchange: "",
                routingKey: "ocrResultsQueue", // Assuming "ocrResultsQueue" for sending results
                basicProperties: null,
                body: resultBytes
            );

            _logger.LogInformation($"OCR result for DocumentId {result.DocumentId} sent to queue.");
        }

        public override void Dispose()
        {
            if (_channel?.IsOpen == true)
            {
                _channel.Close();
                _channel.Dispose();
            }

            if (_connection?.IsOpen == true)
            {
                _connection.Close();
                _connection.Dispose();
            }

            base.Dispose();
        }
    }

    // Message for OCR requests
    public class OcrRequest
    {
        public string DocumentId { get; set; } = string.Empty;
        public string PdfUrl { get; set; } = string.Empty;
    }

    // Message for OCR results
    public class OcrResult
    {
        public string DocumentId { get; set; } = string.Empty;
        public string OcrText { get; set; } = string.Empty;
    }
}
