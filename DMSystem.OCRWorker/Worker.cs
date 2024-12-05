using DMSystem.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;

namespace DMSystem.OCRWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly RabbitMQSetting _rabbitMqSettings;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public Worker(IOptions<RabbitMQSetting> rabbitMqOptions, ILogger<Worker> logger)
        {
            _logger = logger;
            _rabbitMqSettings = rabbitMqOptions.Value;

            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqSettings.HostName,
                UserName = _rabbitMqSettings.UserName,
                Password = _rabbitMqSettings.Password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare the necessary queues
            _channel.QueueDeclare(_rabbitMqSettings.OcrQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueDeclare(_rabbitMqSettings.OcrResultsQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);

            _logger.LogInformation($"Connected to RabbitMQ at {_rabbitMqSettings.HostName}. Queues declared: {_rabbitMqSettings.OcrQueue}, {_rabbitMqSettings.OcrResultsQueue}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OCR Worker started.");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    var ocrRequest = JsonSerializer.Deserialize<OCRRequest>(message);
                    if (ocrRequest != null)
                    {
                        _logger.LogInformation($"Processing OCR for Document ID: {ocrRequest.DocumentId}");

                        var ocrResultText = PerformOcr(ocrRequest.PdfUrl);

                        var resultMessage = new OCRResult
                        {
                            DocumentId = ocrRequest.DocumentId,
                            OcrText = ocrResultText
                        };

                        SendResult(resultMessage);

                        _logger.LogInformation($"OCR completed for Document ID: {ocrRequest.DocumentId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing OCR request.");
                }
            };

            // Start consuming messages from the queue
            _channel.BasicConsume(_rabbitMqSettings.OcrQueue, autoAck: true, consumer);

            // Keep the service alive while listening to RabbitMQ
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private string PerformOcr(string pdfFilePath)
        {
            var result = new StringBuilder();

            using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
            using var img = Pix.LoadFromFile(pdfFilePath);
            using var page = engine.Process(img);
            result.Append(page.GetText());

            return result.ToString();
        }

        private void SendResult(OCRResult result)
        {
            var message = JsonSerializer.Serialize(result);
            var body = Encoding.UTF8.GetBytes(message);

            _channel.BasicPublish("", _rabbitMqSettings.OcrResultsQueue, null, body);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("OCR Worker is stopping.");

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
