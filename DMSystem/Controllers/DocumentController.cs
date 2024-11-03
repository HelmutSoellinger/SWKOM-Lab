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

namespace DMSystem.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly ILogger<DocumentController> _logger;
        private readonly IMapper _mapper;
        private readonly IRabbitMQPublisher<Document> _rabbitMqPublisher;
        private readonly IValidator<Document> _validator;

        public DocumentController(
            IDocumentRepository documentRepository,
            ILogger<DocumentController> logger,
            IMapper mapper,
            IRabbitMQPublisher<Document> rabbitMqPublisher,
            IValidator<Document> validator)
        {
            _documentRepository = documentRepository;
            _logger = logger;
            _mapper = mapper;
            _rabbitMqPublisher = rabbitMqPublisher;
            _validator = validator;
        }

        /// <summary>
        /// Returns all Documents. Possible Filters: name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentDTO>>> Get([FromQuery] string? name)
        {
            var docs = await _documentRepository.GetAllDocumentsAsync(name);
            var docDTOs = _mapper.Map<IEnumerable<DocumentDTO>>(docs);
            return Ok(docDTOs);
        }

        /// <summary>
        /// Creates a new Document with a PDF
        /// </summary>
        /// <param name="createDocumentDto"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<DocumentDTO>> PostDocument(
            [FromForm] Document createDocumentDto,
            [FromForm] IFormFile pdfFile)
        {
            var validationResult = await _validator.ValidateAsync(createDocumentDto);

            if (!validationResult.IsValid)
            {
                return BadRequest(validationResult.Errors);
            }

            var description = createDocumentDto.Description ?? string.Empty;

            byte[] pdfContent;
            using (var memoryStream = new MemoryStream())
            {
                await pdfFile.CopyToAsync(memoryStream);
                pdfContent = memoryStream.ToArray();  // Convert PDF file to byte array
            }

            var newDocument = new Document
            {
                Name = createDocumentDto.Name,
                Author = createDocumentDto.Author,
                LastModified = DateOnly.FromDateTime(DateTime.Today),
                Description = description,
                Content = pdfContent
            };

            await _documentRepository.Add(newDocument);

            var newDocumentDTO = _mapper.Map<DocumentDTO>(newDocument);

            await _rabbitMqPublisher.PublishMessageAsync(newDocument, RabbitMQQueues.OrderValidationQueue);

            return CreatedAtAction(nameof(Get), new { id = newDocument.Id }, newDocumentDTO);
        }

        /// <summary>
        /// Updates a Document via Id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="docDTO"></param>
        /// <returns></returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDocument(int id, DocumentDTO docDTO)
        {
            if (id != docDTO.Id)
            {
                return BadRequest("Document ID mismatch");
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
                return StatusCode(500, "Error updating document");
            }
        }

        /// <summary>
        /// Deletes a Document via Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            var doc = await _documentRepository.GetByIdAsync(id);
            if (doc == null)
            {
                return NotFound();
            }

            await _documentRepository.Remove(doc);
            return NoContent();
        }
    }
}
