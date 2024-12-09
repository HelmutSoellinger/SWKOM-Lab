using Microsoft.AspNetCore.Mvc;
using DMSystem.DAL.Models;
using DMSystem.DAL;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using DMSystem.DTOs;
using DMSystem.Messaging;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using DMSystem.Minio;
using DMSystem.ElasticSearch;

namespace DMSystem.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly ILogger<DocumentController> _logger;
        private readonly IMapper _mapper;
        private readonly IRabbitMQPublisher<OCRRequest> _rabbitMqPublisher;
        private readonly IValidator<DocumentDTO> _validator;
        private readonly IFileStorageService _fileStorageService; 
        private readonly IElasticSearchService _elasticSearchService;

        public DocumentController(
            IDocumentRepository documentRepository,
            ILogger<DocumentController> logger,
            IMapper mapper,
            IRabbitMQPublisher<OCRRequest> rabbitMqPublisher,
            IValidator<DocumentDTO> validator,
            IFileStorageService fileStorageService, 
            IElasticSearchService elasticSearchService
        )
        {
            _documentRepository = documentRepository;
            _logger = logger;
            _mapper = mapper;
            _rabbitMqPublisher = rabbitMqPublisher;
            _validator = validator;
            _fileStorageService = fileStorageService; 
            _elasticSearchService = elasticSearchService;
        }

        /// <summary>
        /// Get all documents or filter by name if provided.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentDTO>>> Get([FromQuery] string? name)
        {
            var docs = await _documentRepository.GetAllDocumentsAsync(name);
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

            // Validate the Document DTO using FluentValidation
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

            // Publish OCR request to RabbitMQ
            var ocrRequest = new OCRRequest
            {
                DocumentId = newDocument.Id.ToString(),
                PdfUrl = objectName
            };

            try
            {
                await _rabbitMqPublisher.PublishMessageAsync(ocrRequest, RabbitMQQueues.OcrQueue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending message to RabbitMQ.");
                return StatusCode(500, new { message = "Error sending OCR request to queue." });
            }

            // Return success response
            var documentDtoResponse = _mapper.Map<DocumentDTO>(newDocument);
            return CreatedAtAction(nameof(GetDocumentById), new { id = newDocument.Id }, documentDtoResponse);
        }

        /// <summary>
        /// Upload a new PDF for an existing document by ID.
        /// </summary>
        [HttpPut("{id}/upload")]
        public async Task<IActionResult> UploadDocumentFile(int id, [FromForm] IFormFile? pdfFile)
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

            // Fetch the existing document
            var existingDocument = await _documentRepository.GetByIdAsync(id);
            if (existingDocument == null)
            {
                return NotFound(new { message = $"Document with ID {id} not found." });
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

            // Update document file path
            existingDocument.FilePath = objectName; // Store MinIO object name
            existingDocument.LastModified = DateTime.UtcNow;
            await _documentRepository.Update(existingDocument);

            // Publish OCR request to RabbitMQ
            var ocrRequest = new OCRRequest
            {
                DocumentId = existingDocument.Id.ToString(),
                PdfUrl = objectName
            };

            try
            {
                await _rabbitMqPublisher.PublishMessageAsync(ocrRequest, RabbitMQQueues.OcrQueue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending message to RabbitMQ.");
                return StatusCode(500, new { message = "Error sending OCR request to queue." });
            }

            return Ok(new { message = $"File uploaded and associated with Document ID {id}." });
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
        /// Checks if a file associated with a document ID exists in the MinIO bucket.
        /// </summary>
        /// <param name="id">The unique ID of the document whose file needs to be checked.</param>
        /// <returns>
        /// HTTP 200 OK if the file exists in MinIO, with a message indicating success.
        /// HTTP 404 Not Found if the file does not exist, with a message indicating the file is not found.
        /// </returns>
        [HttpGet("check-file/{id}")]
        public async Task<IActionResult> CheckFileExists(int id)
        {
            try
            {
                // Retrieve the document by ID
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                {
                    return NotFound(new { message = $"Document with ID {id} not found." });
                }

                // Check if the file exists in MinIO
                var exists = await _fileStorageService.FileExistsAsync(document.FilePath);
                if (exists)
                {
                    return Ok(new { message = "File exists in MinIO." });
                }
                else
                {
                    return NotFound(new { message = "File not found in MinIO." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while checking file for Document ID {id}.");
                return StatusCode(500, new { message = "An unexpected error occurred while checking the file." });
            }
        }

        /// <summary>
        /// Searches documents in Elasticsearch by a given term.
        /// Returns a list of document IDs and the number of matches for the search term in each document.
        /// </summary>
        /// <param name="searchTerm">The term to search for in the document OCR text.</param>
        /// <returns>A list of documents with their IDs and match counts.</returns>
        [HttpPost("search")]
        public async Task<IActionResult> SearchDocuments([FromBody] string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return BadRequest(new { message = "Search term cannot be empty" });
            }

            try
            {
                var searchResults = await _elasticSearchService.SearchDocumentsAsync(searchTerm);

                if (!searchResults.Any())
                {
                    return NotFound(new { message = "No documents found matching the search term." });
                }

                return Ok(searchResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during document search.");
                return StatusCode(500, new { message = "An error occurred while processing the search." });
            }
        }
    }
}
