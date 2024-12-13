using DMSystem.Contracts;
using DMSystem.Contracts.DTOs;
using DMSystem.Minio;
using Microsoft.Extensions.Options;
using System.Text;
using Tesseract;

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

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OCR Worker started. Listening on queue: {Queue}", _ocrQueueName);

            // Set up RabbitMQ consumer for incoming OCR requests
            _rabbitMqService.ConsumeQueue<OCRRequest>(_ocrQueueName, async request =>
            {
                var docId = request.Document.Id;
                try
                {
                    _logger.LogInformation("Processing OCR for Document ID: {DocumentId}", docId);

                    // Perform OCR on the document using the FilePath from DocumentDTO
                    var ocrResultText = await PerformOcrAsync(request.Document.FilePath);

                    // Publish OCR result with full DocumentDTO and OcrText
                    var resultMessage = new OCRResult
                    {
                        Document = request.Document, // Includes Id, Name, Author, LastModified, FilePath
                        OcrText = ocrResultText
                    };

                    await SendResultAsync(resultMessage);

                    _logger.LogInformation("OCR completed for Document ID: {DocumentId}", docId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing OCR request for Document ID: {DocumentId}", docId);
                    // Consider handling retries or DLQs if needed
                }
            });

            return Task.CompletedTask;
        }

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

        private void ConvertPdfToImages(string pdfFilePath, string outputDirectory)
        {
            try
            {
                // Implement your PDF-to-image conversion here
                // e.g. using Ghostscript.NET or another library
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting PDF to images: {PdfFilePath}", pdfFilePath);
                throw;
            }
        }

        public async Task SendResultAsync(OCRResult result)
        {
            try
            {
                await _rabbitMqService.PublishMessageAsync(result, _ocrResultsQueueName);
                _logger.LogInformation("OCR Result published for Document ID: {DocumentId}", result.Document.Id);
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
