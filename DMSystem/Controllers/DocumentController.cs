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

namespace DMSystem.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly ILogger<DocumentController> _logger;
        private readonly IMapper _mapper; // Inject AutoMapper

        public DocumentController(IDocumentRepository documentRepository, ILogger<DocumentController> logger, IMapper mapper)
        {
            _documentRepository = documentRepository; // Inject the repository
            _logger = logger;
            _mapper = mapper;  // Inject AutoMapper
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

            // Map the list of Documents to DocumentDTOs
            var docDTOs = _mapper.Map<IEnumerable<DocumentDTO>>(docs);

            return Ok(docDTOs);
        }

        /// <summary>
        /// Creates a new Document with a PDF
        /// </summary>
        /// <param name="name"></param>
        /// <param name="author"></param>
        /// <param name="description"></param>
        /// <param name="pdfFile"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<DocumentDTO>> PostDocument(
            [FromForm] string name,
            [FromForm] string author,
            [FromForm] string? description,
            [FromForm] IFormFile pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0)
            {
                return BadRequest("No PDF file uploaded.");
            }

            byte[] pdfContent;
            using (var memoryStream = new MemoryStream())
            {
                await pdfFile.CopyToAsync(memoryStream);
                pdfContent = memoryStream.ToArray();  // Convert PDF file to byte array
            }

            // Create a new Document entity
            var newDocument = new Document
            {
                Name = name,
                Author = author,
                LastModified = DateOnly.FromDateTime(DateTime.Today),
                Description = description,
                Content = pdfContent  // Save the binary content
            };

            await _documentRepository.Add(newDocument);  // Add document to the database

            // Map the entity to a DTO
            var newDocumentDTO = _mapper.Map<DocumentDTO>(newDocument);

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

            // Map the DTO to the entity
            _mapper.Map(docDTO, existingDoc); // Updates existingDoc with the properties from docDTO

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
