using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;

namespace DMSystem.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DocumentController : ControllerBase
    {
        private static List<Document> _documents = new List<Document>{
            new Document()
            {
                Id = 1,
                Name = "Test1.pdf",
                LastModified = DateOnly.FromDateTime(DateTime.Today),
                Author = "Max Muster",
                Description = "This is a Test Description",
                Content = "123"
            },
            new Document()
            {
                Id = 2,
                Name = "Test2.pdf",
                LastModified = DateOnly.FromDateTime(DateTime.Today),
                Author = "Anna Muster",
                Description = "This is another Test Description",
                Content = "321"
            }
        };

        private readonly ILogger<DocumentController> _logger;

        public DocumentController(ILogger<DocumentController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Returns all Documents. Possible Filters: name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [HttpGet]
        public IEnumerable<Document> Get([FromQuery] string? name)
        {
            var docs = _documents.AsEnumerable();

            //Nach Name filtern, wenn ein Name übergeben wurde
            if (!string.IsNullOrWhiteSpace(name))
            {
                docs = docs.Where(t => t.Name.Contains(name));
            }

            return docs;
        }

        /// <summary>
        /// Creates a new Document
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult<Document> PostDocument(Document doc)
        {
            doc.Id = _documents.Max(t => t.Id) + 1;                         // Neue ID generieren
            _documents.Add(doc);                                            // Item zur Liste hinzufügen
            return CreatedAtAction(nameof(Get), new { id = doc.Id }, doc);
        }

        /// <summary>
        /// Updates a Document via Id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="document"></param>
        /// <returns></returns>
        [HttpPut("{id}")]
        public IActionResult PutDocument(int id, Document document)
        {
            var existingDoc = _documents.FirstOrDefault(t => t.Id == id);
            if (existingDoc == null)
            {
                return NotFound();
            }

            existingDoc.Name = document.Name;
            existingDoc.LastModified = document.LastModified;
            existingDoc.Author = document.Author;
            existingDoc.Description = document.Description;
            existingDoc.Content = document.Content;
            return NoContent();
        }


        /// <summary>
        /// Deletes a Document via Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public IActionResult DeleteDocument(int id)
        {
            var doc = _documents.FirstOrDefault(t => t.Id == id);
            if (doc == null)
            {
                return NotFound();
            }

            _documents.Remove(doc);
            return NoContent();
        }
    }
}
