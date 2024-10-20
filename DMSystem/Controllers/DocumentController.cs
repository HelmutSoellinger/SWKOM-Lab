using Microsoft.AspNetCore.Mvc;
using DMSystem.DAL.Models;
using DMSystem.DAL;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DMSystem.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly ILogger<DocumentController> _logger;

        public DocumentController(IDocumentRepository documentRepository, ILogger<DocumentController> logger)
        {
            _documentRepository = documentRepository; // Inject the repository
            _logger = logger;
        }

        /// <summary>
        /// Returns all Documents. Possible Filters: name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Document>>> Get([FromQuery] string? name)
        {
            var docs = await _documentRepository.GetAllDocumentsAsync(name);
            return Ok(docs);
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
        public async Task<ActionResult<Document>> PostDocument(
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

            var newDocument = new Document
            {
                Name = name,
                Author = author,
                LastModified = DateOnly.FromDateTime(DateTime.Today),
                Description = description,
                Content = pdfContent  // Save the binary content
            };

            await _documentRepository.Add(newDocument);  // Add document to the database

            return CreatedAtAction(nameof(Get), new { id = newDocument.Id }, newDocument);
        }

        /// <summary>
        /// Updates a Document via Id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="doc"></param>
        /// <returns></returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDocument(int id, Document doc)
        {
            if (id != doc.Id)
            {
                return BadRequest("Document ID mismatch");
            }

            var existingDoc = await _documentRepository.GetByIdAsync(id);
            if (existingDoc == null)
            {
                return NotFound();
            }

            existingDoc.Name = doc.Name;
            existingDoc.LastModified = doc.LastModified;
            existingDoc.Author = doc.Author;
            existingDoc.Description = doc.Description;
            existingDoc.Content = doc.Content;

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
