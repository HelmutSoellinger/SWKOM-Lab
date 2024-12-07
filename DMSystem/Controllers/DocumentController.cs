﻿using Microsoft.AspNetCore.Mvc;
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
        private readonly IRabbitMQPublisher<Document> _rabbitMqPublisher;
        private readonly IValidator<DocumentDTO> _validator;

        public DocumentController(
            IDocumentRepository documentRepository,
            ILogger<DocumentController> logger,
            IMapper mapper,
            IRabbitMQPublisher<Document> rabbitMqPublisher,
            IValidator<DocumentDTO> validator)
        {
            _documentRepository = documentRepository;
            _logger = logger;
            _mapper = mapper;
            _rabbitMqPublisher = rabbitMqPublisher;
            _validator = validator;
        }

        /// <summary>
        /// Returns all Documents. Optional filter by name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>List of Documents</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentDTO>>> Get([FromQuery] string? name)
        {
            var docs = await _documentRepository.GetAllDocumentsAsync(name);
            var docDTOs = _mapper.Map<IEnumerable<DocumentDTO>>(docs);
            return Ok(docDTOs);
        }

        /// <summary>
        /// Creates a new Document with an associated PDF file.
        /// </summary>
        /// <param name="createDocumentDto">Document information</param>
        /// <param name="pdfFile">PDF file</param>
        /// <returns>Created Document</returns>
        [HttpPost]
        public async Task<ActionResult<DocumentDTO>> PostDocument(
            [FromForm] DocumentDTO createDocument,
            [FromForm] IFormFile pdfFile)
        {
            // Validate the DTO
            var validationResult = await _validator.ValidateAsync(createDocument);

            if (!validationResult.IsValid)
            {
                // Prepare structured error response with property names and messages
                var errorMessages = validationResult.Errors
                    .Select(error => new
                    {
                        Property = error.PropertyName,
                        Message = error.ErrorMessage
                    })
                    .ToList();

                return BadRequest(new { errors = errorMessages });
            }

            // Check for the PDF file
            if (pdfFile == null || pdfFile.Length == 0)
            {
                return BadRequest(new { errors = new[] { new { Property = "pdfFile", Message = "A PDF file is required." } } });
            }

            // Process the file and add the document to the repository
            byte[] pdfContent;
            using (var memoryStream = new MemoryStream())
            {
                await pdfFile.CopyToAsync(memoryStream);
                pdfContent = memoryStream.ToArray();
            }

            var newDocument = _mapper.Map<Document>(createDocument);
            newDocument.Content = pdfContent;
            newDocument.LastModified = DateOnly.FromDateTime(DateTime.Today);
            if(newDocument.Description == null)
            {
                newDocument.Description = string.Empty;
            }

            await _documentRepository.Add(newDocument);

            // Publish message to RabbitMQ
            await _rabbitMqPublisher.PublishMessageAsync(newDocument, RabbitMQQueues.OrderValidationQueue);

            // Map and return the created document
            var newDocumentDTO = _mapper.Map<DocumentDTO>(newDocument);
            return CreatedAtAction(nameof(Get), new { id = newDocument.Id }, newDocumentDTO);
        }

        /// <summary>
        /// Updates a Document by Id
        /// </summary>
        /// <param name="id">Document Id</param>
        /// <param name="docDTO">Updated Document data</param>
        /// <returns>Status code indicating success or failure</returns>
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
        /// Deletes a Document by Id
        /// </summary>
        /// <param name="id">Document Id</param>
        /// <returns>Status code indicating success or failure</returns>
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
