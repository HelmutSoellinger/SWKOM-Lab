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

            // Get OCR queue name from configuration
            _ocrQueueName = rabbitMqSettings.Value.Queues["OcrQueue"];
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
            // Validate uploaded file
            if (pdfFile == null || pdfFile.Length == 0)
            {
                ModelState.AddModelError("pdfFile", "No file uploaded.");
                return BadRequest(ModelState);
            }
            if (!pdfFile.FileName.EndsWith(".pdf"))
            {
                ModelState.AddModelError("pdfFile", "Only PDF files are allowed.");
                return BadRequest(ModelState);
            }

            // Validate the Document DTO
            var validationResult = await _validator.ValidateAsync(documentDto);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .Select(e => new { Property = e.PropertyName, Message = e.ErrorMessage })
                    .ToList();
                return BadRequest(new { errors });
            }

            // Save the PDF file to MinIO
            string objectName = Guid.NewGuid() + "_" + pdfFile.FileName;
            using var fileStream = pdfFile.OpenReadStream();
            try
            {
                await _fileStorageService.UploadFileAsync(objectName, fileStream, pdfFile.Length, pdfFile.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to MinIO.");
                return StatusCode(500, new { message = "Error uploading file to MinIO storage." });
            }

            // Create a new document in the DAL
            var newDocument = _mapper.Map<Document>(documentDto);
            newDocument.FilePath = objectName; // Store MinIO object name
            newDocument.LastModified = DateTime.UtcNow;
            await _documentRepository.Add(newDocument);

            // Map back to DTO to send in OCR request
            var documentDtoResponse = _mapper.Map<DocumentDTO>(newDocument);

            // Publish OCR request using DocumentDTO
            var ocrRequest = new OCRRequest
            {
                Document = documentDtoResponse
            };

            try
            {
                await _rabbitMqService.PublishMessageAsync(ocrRequest, _ocrQueueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending message to RabbitMQ.");
                return StatusCode(500, new { message = "Error sending OCR request to queue." });
            }

            // Return success response
            return CreatedAtAction(nameof(GetDocumentById), new { id = newDocument.Id }, documentDtoResponse);
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

            // Update Name and Author (Validation)
            if (string.IsNullOrWhiteSpace(documentDto.Name) || string.IsNullOrWhiteSpace(documentDto.Author))
            {
                return BadRequest(new { message = "Name and Author cannot be empty." });
            }

            existingDocument.Name = documentDto.Name;
            existingDocument.Author = documentDto.Author;

            // Handle file upload and old file deletion
            if (pdfFile != null && pdfFile.Length > 0)
            {
                if (!pdfFile.FileName.EndsWith(".pdf"))
                {
                    return BadRequest(new { message = "Only PDF files are allowed." });
                }

                try
                {
                    // Delete old file from storage
                    await _fileStorageService.DeleteFileAsync(existingDocument.FilePath);

                    // Save new file to MinIO
                    string objectName = Guid.NewGuid() + "_" + pdfFile.FileName;
                    using var fileStream = pdfFile.OpenReadStream();
                    await _fileStorageService.UploadFileAsync(objectName, fileStream, pdfFile.Length, pdfFile.ContentType);

                    // Update document path
                    existingDocument.FilePath = objectName;

                    // Trigger OCR since a new file was uploaded
                    var ocrRequest = new OCRRequest
                    {
                        Document = _mapper.Map<DocumentDTO>(existingDocument)
                    };
                    await _rabbitMqService.PublishMessageAsync(ocrRequest, _ocrQueueName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file upload.");
                    return StatusCode(500, new { message = "Error uploading file to storage." });
                }
            }

            existingDocument.LastModified = DateTime.UtcNow;

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
                return NotFound();
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

            await _documentRepository.Remove(doc);
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
    }
}
