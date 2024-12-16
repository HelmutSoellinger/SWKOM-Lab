using Microsoft.AspNetCore.Mvc;
using DMSystem.DAL.Models;
using DMSystem.DAL;
using AutoMapper;
using DMSystem.Contracts.DTOs;
using FluentValidation;
using DMSystem.Minio;
using DMSystem.ElasticSearch;
using Microsoft.Extensions.Options;
using DMSystem.Contracts;
using Microsoft.AspNetCore.StaticFiles;

namespace DMSystem.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly ILogger<DocumentController> _logger;
        private readonly IMapper _mapper;
        private readonly IRabbitMQService _rabbitMqService;
        private readonly IValidator<DocumentDTO> _validator;
        private readonly IMinioFileStorageService _fileStorageService;
        private readonly IElasticSearchService _elasticSearchService;
        private readonly string _ocrQueueName;
        private readonly string _deleteQueueName;

        public DocumentController(
            IDocumentRepository documentRepository,
            ILogger<DocumentController> logger,
            IMapper mapper,
            IRabbitMQService rabbitMqService,
            IValidator<DocumentDTO> validator,
            IMinioFileStorageService fileStorageService,
            IElasticSearchService elasticSearchService,
            IOptions<RabbitMQSettings> rabbitMqSettings
        )
        {
            _documentRepository = documentRepository;
            _logger = logger;
            _mapper = mapper;
            _rabbitMqService = rabbitMqService;
            _validator = validator;
            _fileStorageService = fileStorageService;
            _elasticSearchService = elasticSearchService;

            // RabbitMQ Queue configuration
            _ocrQueueName = rabbitMqSettings.Value.Queues["OcrQueue"];
            _deleteQueueName = rabbitMqSettings.Value.Queues["DeleteQueue"];
        }

        /// <summary>
        /// Get all documents.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentDTO>>> Get()
        {
            // No name filtering, simply return all documents
            var docs = await _documentRepository.GetAllDocumentsAsync();
            var docDTOs = _mapper.Map<IEnumerable<DocumentDTO>>(docs);
            return Ok(docDTOs);
        }

        /// <summary>
        /// Create a new document with an uploaded PDF file.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateDocument([FromForm] DocumentDTO documentDto, [FromForm] IFormFile? pdfFile)
        {
            try
            {
                // Validate the Document DTO
                var validationResult = await _validator.ValidateAsync(documentDto);
                if (!validationResult.IsValid)
                {
                    var errors = validationResult.Errors
                        .Select(e => new { Property = e.PropertyName, Message = e.ErrorMessage })
                        .ToList();
                    return BadRequest(new { errors });
                }

                // Upload the file and get the file path
                string objectName = await UploadFileToStorageAsync(pdfFile);

                // Create a new document in the DAL
                var newDocument = _mapper.Map<Document>(documentDto);
                newDocument.FilePath = objectName; // Store MinIO object name
                newDocument.LastModified = DateTime.UtcNow;
                await _documentRepository.Add(newDocument);

                // Publish OCR message
                await PublishOcrMessageAsync(newDocument);

                // Map back to DTO and return the response
                var documentDtoResponse = _mapper.Map<DocumentDTO>(newDocument);
                return CreatedAtAction(nameof(GetDocumentById), new { id = newDocument.Id }, documentDtoResponse);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Upload a new PDF for an existing document by ID.
        /// </summary>
        [HttpPut("{id}/upload")]
        public async Task<IActionResult> UploadDocumentFile(int id, [FromForm] DocumentDTO documentDto, [FromForm] IFormFile? pdfFile)
        {
            var existingDocument = await _documentRepository.GetByIdAsync(id);
            if (existingDocument == null)
            {
                return NotFound(new { message = $"Document with ID {id} not found." });
            }

            // Validate Name and Author
            if (string.IsNullOrWhiteSpace(documentDto.Name) || string.IsNullOrWhiteSpace(documentDto.Author))
            {
                return BadRequest(new { message = "Name and Author cannot be empty." });
            }

            existingDocument.Name = documentDto.Name;
            existingDocument.Author = documentDto.Author;

            // Handle file upload and old file deletion
            if (pdfFile != null && pdfFile.Length > 0)
            {
                try
                {
                    // Validate the uploaded file
                    string newFilePath = await UploadFileToStorageAsync(pdfFile);

                    // Delete old file from storage
                    if (!string.IsNullOrWhiteSpace(existingDocument.FilePath))
                    {
                        try
                        {
                            await _fileStorageService.DeleteFileAsync(existingDocument.FilePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error deleting old file '{existingDocument.FilePath}' from storage.");
                            return StatusCode(500, new { message = "Error deleting old file from storage." });
                        }
                    }

                    // Update document path with the new file
                    existingDocument.FilePath = newFilePath;

                    // Trigger OCR since a new file was uploaded
                    await PublishOcrMessageAsync(existingDocument);
                }
                catch (ArgumentException ex)
                {
                    return BadRequest(new { message = ex.Message });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file upload.");
                    return StatusCode(500, new { message = "Error uploading file to storage." });
                }
            }

            // Update the last modified timestamp
            existingDocument.LastModified = DateTime.UtcNow;

            // Save the updated document
            await _documentRepository.Update(existingDocument);

            return Ok(new { message = $"Document ID {id} updated successfully and OCR triggered." });
        }

        /// <summary>
        /// Delete a document by ID.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            var doc = await _documentRepository.GetByIdAsync(id);
            if (doc == null)
            {
                return NotFound(new { message = $"Document with ID {id} not found." });
            }

            // Delete the file from MinIO
            try
            {
                await _fileStorageService.DeleteFileAsync(doc.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting file '{doc.FilePath}' from MinIO.");
                return StatusCode(500, new { message = "Error deleting file from MinIO storage." });
            }

            // Publish delete request to RabbitMQ
            try
            {
                var deleteMessage = new DeleteDocumentMessage { DocumentId = id };
                await _rabbitMqService.PublishMessageAsync(deleteMessage, _deleteQueueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error publishing delete message for Document ID {id} to RabbitMQ.");
                return StatusCode(500, new { message = "Error publishing delete message to RabbitMQ." });
            }

            // Remove the document from the repository
            try
            {
                await _documentRepository.Remove(doc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing Document ID {id} from the database.");
                return StatusCode(500, new { message = "Error removing document from the database." });
            }

            return NoContent();
        }

        /// <summary>
        /// Get a document by its ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocumentById(int id)
        {
            try
            {
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                {
                    return NotFound(new { message = $"Document with ID {id} not found." });
                }

                var documentDto = _mapper.Map<DocumentDTO>(document);
                return Ok(documentDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while retrieving the document with ID {id}.");
                return StatusCode(500, new { message = "An unexpected error occurred while retrieving the document." });
            }
        }

        /// <summary>
        /// Searches documents in Elasticsearch by a given term.
        /// </summary>
        [HttpPost("search")]
        public async Task<IActionResult> SearchDocuments([FromBody] string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return BadRequest(new { message = "Search term cannot be empty" });
            }

            try
            {
                _logger.LogInformation("Searching for term: {SearchTerm}", searchTerm);

                var searchResults = await _elasticSearchService.SearchDocumentsAsync(searchTerm);

                if (!searchResults.Any())
                {
                    _logger.LogInformation("No results found for term: {SearchTerm}", searchTerm);
                    return NotFound(new { message = "No documents found matching the search term." });
                }

                _logger.LogInformation("Search results count: {Count}", searchResults.Count());
                return Ok(searchResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during document search for term: {SearchTerm}", searchTerm);
                return StatusCode(500, new { message = "An error occurred while processing the search." });
            }
        }

        /// <summary>
        /// Download a document file by ID.
        /// </summary>
        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadDocument(int id)
        {
            try
            {
                // Retrieve the document details from the repository
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                {
                    return NotFound(new { message = $"Document with ID {id} not found." });
                }

                // Retrieve the file from MinIO using the file path
                var fileStream = await _fileStorageService.DownloadFileAsync(document.FilePath);
                if (fileStream == null)
                {
                    return NotFound(new { message = $"File '{document.FilePath}' not found in MinIO storage." });
                }

                // Get content type for the file
                var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(document.FilePath, out var contentType))
                {
                    contentType = "application/octet-stream"; // Fallback content type
                }

                // Return the file as a download
                return File(fileStream, contentType, Path.GetFileName(document.FilePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while downloading document with ID {id}.");
                return StatusCode(500, new { message = "An unexpected error occurred while downloading the document." });
            }
        }

        private async Task<string> UploadFileToStorageAsync(IFormFile pdfFile)
        {
            // Validate uploaded file
            if (pdfFile == null || pdfFile.Length == 0)
                throw new ArgumentException("No file uploaded.");

            if (!pdfFile.FileName.EndsWith(".pdf"))
                throw new ArgumentException("Only PDF files are allowed.");

            // Generate a unique file name
            string objectName = Guid.NewGuid() + "_" + pdfFile.FileName;

            // Upload file to storage
            using var fileStream = pdfFile.OpenReadStream();
            try
            {
                await _fileStorageService.UploadFileAsync(objectName, fileStream, pdfFile.Length, pdfFile.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to MinIO.");
                throw new Exception("Error uploading file to MinIO storage.", ex);
            }

            return objectName;
        }

        private async Task PublishOcrMessageAsync(Document document)
        {
            var ocrRequest = new OCRRequest
            {
                Document = _mapper.Map<DocumentDTO>(document)
            };

            try
            {
                await _rabbitMqService.PublishMessageAsync(ocrRequest, _ocrQueueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending message to RabbitMQ.");
                throw new Exception("Error sending OCR request to queue.", ex);
            }
        }
    }
}
