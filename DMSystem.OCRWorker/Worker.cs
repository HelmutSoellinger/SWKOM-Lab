using DMSystem.Messaging;
using DMSystem.Minio;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private readonly IRabbitMQService _rabbitMqService;
        private readonly MinioFileStorageService _fileStorageService;
        private readonly string _ocrQueueName;
        private readonly string _ocrResultsQueueName;

        /// <summary>
        /// Initializes the worker with required services and configurations.
        /// </summary>
        /// <param name="rabbitMqSettings">RabbitMQ settings injected via IOptions.</param>
        /// <param name="fileStorageService">MinIO file storage service for managing files.</param>
        /// <param name="rabbitMqService">Centralized RabbitMQ service.</param>
        /// <param name="logger">Logger for logging operations.</param>
        public Worker(
            IOptions<RabbitMQSettings> rabbitMqSettings,
            MinioFileStorageService fileStorageService,
            IRabbitMQService rabbitMqService,
            ILogger<Worker> logger)
        {
            _logger = logger;
            _rabbitMqService = rabbitMqService;
            _fileStorageService = fileStorageService;

            // Extract queue names from config
            _ocrQueueName = rabbitMqSettings.Value.Queues["OcrQueue"];
            _ocrResultsQueueName = rabbitMqSettings.Value.Queues["OcrResultsQueue"];
        }

        /// <summary>
        /// Executes the background service for processing OCR messages.
        /// </summary>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OCR Worker started. Listening on queue: {Queue}", _ocrQueueName);

            // Set up RabbitMQ consumer for incoming OCR requests
            _rabbitMqService.ConsumeQueue<OCRRequest>(_ocrQueueName, async request =>
            {
                try
                {
                    _logger.LogInformation("Processing OCR for Document ID: {DocumentId}", request.DocumentId);

                    // Perform OCR on the document
                    var ocrResultText = await PerformOcrAsync(request.PdfUrl);

                    // Publish OCR result
                    var resultMessage = new OCRResult
                    {
                        DocumentId = request.DocumentId,
                        OcrText = ocrResultText
                    };
                    await SendResultAsync(resultMessage);

                    _logger.LogInformation("OCR completed for Document ID: {DocumentId}", request.DocumentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing OCR request for Document ID: {DocumentId}", request.DocumentId);
                    // Consider handling retries or DLQs if needed
                }
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs OCR on a file stored in MinIO.
        /// </summary>
        /// <param name="objectName">The name of the file in MinIO.</param>
        /// <returns>The extracted text from the file.</returns>
        public async Task<string> PerformOcrAsync(string objectName)
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
                _logger.LogError(ex, "Error performing OCR on file: {ObjectName}", objectName);
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
        /// Replace this placeholder with actual PDF-to-image conversion logic.
        /// </summary>
        private void ConvertPdfToImages(string pdfFilePath, string outputDirectory)
        {
            try
            {
                // Implement your PDF-to-image conversion here
                // Example: Ghostscript.NET, ImageMagick, or any suitable library
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting PDF to images: {PdfFilePath}", pdfFilePath);
                throw;
            }
        }

        /// <summary>
        /// Publishes the OCR result to the RabbitMQ results queue.
        /// </summary>
        /// <param name="result">The OCR result to send.</param>
        public async Task SendResultAsync(OCRResult result)
        {
            try
            {
                await _rabbitMqService.PublishMessageAsync(result, _ocrResultsQueueName);
                _logger.LogInformation("OCR Result published for Document ID: {DocumentId}", result.DocumentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing OCR result.");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("OCR Worker is stopping.");
            await base.StopAsync(cancellationToken);
        }
    }
}
