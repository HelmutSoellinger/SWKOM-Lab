using DMSystem.DAL;
using DMSystem.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DMSystem.Tests
{
    public class DocumentRepositoryTests
    {
        private DALContext GetInMemoryDbContext(string databaseName)
        {
            var options = new DbContextOptionsBuilder<DALContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            return new DALContext(options);
        }

        [Fact]
        public async Task AddDocument_ShouldAddNewDocument()
        {
            // Arrange
            var context = GetInMemoryDbContext("AddDocument_TestDatabase");
            var repository = new DocumentRepository(context);

            var document = new Document
            {
                Name = "Test Document",
                LastModified = DateOnly.FromDateTime(System.DateTime.Now),
                Author = "Test Author",
                Description = "Test Description",
                Content = new byte[] { 1, 2, 3, 4 }
            };

            // Act
            var result = await repository.Add(document);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Document", result.Name);
            Assert.Equal("Test Author", result.Author);
        }

        [Fact]
        public async Task GetAllDocuments_ShouldReturnAllDocuments()
        {
            // Arrange
            var context = GetInMemoryDbContext("GetAllDocuments_TestDatabase");
            var repository = new DocumentRepository(context);

            var documents = new List<Document>
            {
                new Document { Name = "Document 1", LastModified = DateOnly.FromDateTime(System.DateTime.Now), Author = "Author 1", Content = new byte[] { 1, 2 } },
                new Document { Name = "Document 2", LastModified = DateOnly.FromDateTime(System.DateTime.Now), Author = "Author 2", Content = new byte[] { 3, 4 } }
            };

            foreach (var doc in documents)
            {
                await repository.Add(doc);
            }

            // Act
            var result = await repository.GetAllDocumentsAsync(null);

            // Assert
            Assert.Equal(2, result.Count); // Expecting 2 documents
        }

        [Fact]
        public async Task GetDocumentById_ShouldReturnCorrectDocument()
        {
            // Arrange
            var context = GetInMemoryDbContext("GetDocumentById_TestDatabase");
            var repository = new DocumentRepository(context);

            var document = new Document
            {
                Name = "Document for ID Test",
                LastModified = DateOnly.FromDateTime(System.DateTime.Now),
                Author = "Test Author",
                Content = new byte[] { 1, 2, 3 }
            };

            var addedDocument = await repository.Add(document);

            // Act
            var result = await repository.GetByIdAsync(addedDocument.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Document for ID Test", result.Name);
            Assert.Equal("Test Author", result.Author);
        }

        [Fact]
        public async Task UpdateDocument_ShouldUpdateDocument()
        {
            // Arrange
            var context = GetInMemoryDbContext("UpdateDocument_TestDatabase");
            var repository = new DocumentRepository(context);

            var document = new Document
            {
                Name = "Document to Update",
                LastModified = DateOnly.FromDateTime(System.DateTime.Now),
                Author = "Initial Author",
                Content = new byte[] { 1, 2, 3 }
            };

            var addedDocument = await repository.Add(document);

            // Act
            addedDocument.Author = "Updated Author";
            var updatedDocument = await repository.Update(addedDocument);

            // Assert
            Assert.Equal("Updated Author", updatedDocument.Author);
        }

        [Fact]
        public async Task RemoveDocument_ShouldRemoveDocument()
        {
            // Arrange
            var context = GetInMemoryDbContext("RemoveDocument_TestDatabase");
            var repository = new DocumentRepository(context);

            var document = new Document
            {
                Name = "Document to Remove",
                LastModified = DateOnly.FromDateTime(System.DateTime.Now),
                Author = "Test Author",
                Content = new byte[] { 1, 2, 3 }
            };

            var addedDocument = await repository.Add(document);

            // Act
            await repository.Remove(addedDocument);

            // Assert
            var result = await repository.GetByIdAsync(addedDocument.Id);
            Assert.Null(result); // Document should be null after removal
        }
    }
}
