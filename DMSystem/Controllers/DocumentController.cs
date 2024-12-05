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

        public DocumentController(
            IDocumentRepository documentRepository,
            ILogger<DocumentController> logger,
            IMapper mapper,
            IRabbitMQPublisher<OCRRequest> rabbitMqPublisher,
            IValidator<DocumentDTO> validator)
        {
            _documentRepository = documentRepository;
            _logger = logger;
            _mapper = mapper;
            _rabbitMqPublisher = rabbitMqPublisher;
            _validator = validator;
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

            // Save the PDF file
            var filePath = Path.Combine("UploadedFiles", Guid.NewGuid() + "_" + pdfFile.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await pdfFile.CopyToAsync(stream);
            }

            // Create a new document in the DAL
            var newDocument = _mapper.Map<Document>(documentDto);
            newDocument.FilePath = filePath;
            newDocument.LastModified = DateTime.UtcNow;
            await _documentRepository.Add(newDocument);

            // Publish OCR request to RabbitMQ
            var ocrRequest = new OCRRequest
            {
                DocumentId = newDocument.Id.ToString(),
                PdfUrl = filePath
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

            // Save the PDF file
            var filePath = Path.Combine("UploadedFiles", Guid.NewGuid() + "_" + pdfFile.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await pdfFile.CopyToAsync(stream);
            }

            // Update document file path
            existingDocument.FilePath = filePath;
            existingDocument.LastModified = DateTime.UtcNow;
            await _documentRepository.Update(existingDocument);

            // Publish OCR request to RabbitMQ
            var ocrRequest = new OCRRequest
            {
                DocumentId = existingDocument.Id.ToString(),
                PdfUrl = filePath
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
        /// Update an existing document by ID.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDocument(int id, DocumentDTO docDTO)
        {
            if (id != docDTO.Id)
            {
                return BadRequest("Document ID mismatch.");
            }

            var existingDoc = await _documentRepository.GetByIdAsync(id);
            if (existingDoc == null)
            {
                return NotFound();
            }

            _mapper.Map(docDTO, existingDoc);

            try
            {
                await _documentRepository.Update(existingDoc);
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogError($"Concurrency error while updating document with ID {id}");
                return StatusCode(500, "Error updating document due to concurrency issues.");
            }
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

            // Delete the file from the server
            if (!string.IsNullOrEmpty(doc.FilePath) && System.IO.File.Exists(doc.FilePath))
            {
                System.IO.File.Delete(doc.FilePath);
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
                // Use the repository to fetch the document
                var document = await _documentRepository.GetByIdAsync(id);
                if (document == null)
                {
                    return NotFound(new { message = $"Document with ID {id} not found." });
                }

                // Map the document to its DTO representation
                var documentDto = _mapper.Map<DocumentDTO>(document);

                // Return the document DTO in the response
                return Ok(documentDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while retrieving the document with ID {id}.");
                return StatusCode(500, new { message = "An unexpected error occurred while retrieving the document." });
            }
        }
    }
}
