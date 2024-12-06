using DMSystem.Messaging;
using DMSystem.Minio;
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
using System.IO;

namespace DMSystem.OCRWorker
{
    /// <summary>
    /// Background service for processing OCR requests.
    /// </summary>
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly RabbitMQSetting _rabbitMqSettings;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly MinioFileStorageService _fileStorageService;

        /// <summary>
        /// Initializes the worker with required services and configurations.
        /// </summary>
        /// <param name="rabbitMqOptions">RabbitMQ settings injected via IOptions.</param>
        /// <param name="fileStorageService">MinIO file storage service for managing files.</param>
        /// <param name="logger">Logger for logging operations.</param>
        public Worker(
            IOptions<RabbitMQSetting> rabbitMqOptions,
            MinioFileStorageService fileStorageService,
            ILogger<Worker> logger)
        {
            _logger = logger;
            _rabbitMqSettings = rabbitMqOptions.Value;
            _fileStorageService = fileStorageService;

            // Initialize RabbitMQ connection and channel
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqSettings.HostName,
                UserName = _rabbitMqSettings.UserName,
                Password = _rabbitMqSettings.Password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare queues for OCR requests and results
            _channel.QueueDeclare(_rabbitMqSettings.OcrQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueDeclare(_rabbitMqSettings.OcrResultsQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);

            _logger.LogInformation($"Connected to RabbitMQ at {_rabbitMqSettings.HostName}. Queues declared: {_rabbitMqSettings.OcrQueue}, {_rabbitMqSettings.OcrResultsQueue}");
        }

        /// <summary>
        /// Executes the background service for processing OCR messages.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OCR Worker started.");

            // Set up RabbitMQ consumer for incoming OCR requests
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    // Deserialize the OCR request
                    var ocrRequest = JsonSerializer.Deserialize<OCRRequest>(message);
                    if (ocrRequest != null)
                    {
                        _logger.LogInformation($"Processing OCR for Document ID: {ocrRequest.DocumentId}");

                        // Perform OCR on the document
                        var ocrResultText = await PerformOcrAsync(ocrRequest.PdfUrl);

                        // Publish OCR result to the results queue
                        var resultMessage = new OCRResult
                        {
                            DocumentId = ocrRequest.DocumentId,
                            OcrText = ocrResultText
                        };

                        // Publish the result
                        SendResult(resultMessage);

                        _logger.LogInformation($"OCR completed for Document ID: {ocrRequest.DocumentId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing OCR request.");
                }
            };

            // Start consuming messages from the OCR queue
            _channel.BasicConsume(_rabbitMqSettings.OcrQueue, autoAck: true, consumer);

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        /// <summary>
        /// Performs OCR on a file stored in MinIO.
        /// </summary>
        /// <param name="objectName">The name of the file in MinIO.</param>
        /// <returns>The extracted text from the file.</returns>
        private async Task<string> PerformOcrAsync(string objectName)
        {
            var result = new StringBuilder();
            var tempPdfPath = $"/tmp/{objectName}";
            var outputImageDir = "/tmp/pdf_images";

            try
            {
                // Download the file from MinIO
                var fileStream = await _fileStorageService.DownloadFileAsync(objectName);

                // Save the file locally for conversion
                using (var file = new FileStream(tempPdfPath, FileMode.Create, FileAccess.Write))
                {
                    await fileStream.CopyToAsync(file);
                }

                // Convert the PDF to images
                Directory.CreateDirectory(outputImageDir);
                ConvertPdfToImages(tempPdfPath, outputImageDir);

                // Perform OCR on each image
                using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
                foreach (var imagePath in Directory.GetFiles(outputImageDir, "*.png"))
                {
                    using var img = Pix.LoadFromFile(imagePath);
                    using var page = engine.Process(img);
                    result.Append(page.GetText());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error performing OCR on file: {objectName}");
                throw;
            }
            finally
            {
                // Clean up temporary files
                if (Directory.Exists(outputImageDir))
                    Directory.Delete(outputImageDir, true);
                if (File.Exists(tempPdfPath))
                    File.Delete(tempPdfPath);
            }

            return result.ToString();
        }

        /// <summary>
        /// Converts a PDF file into individual images for OCR processing.
        /// </summary>
        /// <param name="pdfFilePath">The path to the PDF file.</param>
        /// <param name="outputDirectory">The directory to store the output images.</param>
        private void ConvertPdfToImages(string pdfFilePath, string outputDirectory)
        {
            try
            {
                // Use a PDF-to-image conversion library (e.g., Ghostscript.NET)
                // Replace this placeholder with the actual conversion logic
                // Each page of the PDF should be converted to an image stored in outputDirectory
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error converting PDF to images: {pdfFilePath}");
                throw;
            }
        }

        /// <summary>
        /// Publishes the OCR result to the RabbitMQ results queue.
        /// </summary>
        /// <param name="result">The OCR result to send.</param>
        private void SendResult(OCRResult result)
        {
            try
            {
                var message = JsonSerializer.Serialize(result);
                var body = Encoding.UTF8.GetBytes(message);

                // Publish to the result queue
                _channel.BasicPublish(
                    exchange: "",
                    routingKey: _rabbitMqSettings.OcrResultsQueue,
                    basicProperties: null,
                    body: body);

                _logger.LogInformation($"OCR Result published to queue for Document ID: {result.DocumentId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing OCR result.");
                throw;
            }
        }

        /// <summary>
        /// Cleans up resources when stopping the service.
        /// </summary>
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

        /// <summary>
        /// Disposes of the RabbitMQ connection and channel.
        /// </summary>
        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}
