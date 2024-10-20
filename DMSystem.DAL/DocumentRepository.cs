using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; // Required for EF Core
using DMSystem.DAL.Models;

namespace DMSystem.DAL
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly DALContext _context;

        public DocumentRepository(DALContext context)
        {
            _context = context; // Store the database context
        }

        // Add Document
        public async Task<Document> Add(Document document)
        {
            await _context.Documents.AddAsync(document);
            await _context.SaveChangesAsync(); // Save changes to the database
            return document; // Return the added document
        }

        // Update Document
        public async Task<Document> Update(Document document)
        {
            _context.Documents.Update(document);
            await _context.SaveChangesAsync(); // Save changes to the database
            return document; // Return the updated document
        }

        // Remove Document
        public async Task Remove(Document document)
        {
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync(); // Save changes to the database
        }

        // Get Documents by Name
        public async Task<ICollection<Document>> GetDocumentsByName(string searchPattern)
        {
            // Find documents that match the given name
            return await _context.Documents
                .Where(d => d.Name.Contains(searchPattern)) // Return all matching documents
                .ToListAsync();
        }

        // Get All Documents
        public async Task<ICollection<Document>> GetAllDocumentsAsync(string? name)
        {
            var query = _context.Documents.AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(d => d.Name.Contains(name)); // Apply filtering if a name is provided
            }

            return await query.ToListAsync(); // Return all documents as a list
        }

        // New method to get a document by Id
        public async Task<Document?> GetByIdAsync(int id)
        {
            return await _context.Documents.FindAsync(id); // Find document by Id
        }
    }

    // IDocumentRepository interface
    public interface IDocumentRepository
    {
        Task<Document> Add(Document document);
        Task<Document> Update(Document document);
        Task Remove(Document document);
        Task<Document?> GetByIdAsync(int id); // New method to get a document by Id
        Task<ICollection<Document>> GetDocumentsByName(string searchPattern);
        Task<ICollection<Document>> GetAllDocumentsAsync(string? name); // New method for fetching documents
    }
}
