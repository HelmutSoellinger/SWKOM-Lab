using DMSystem.DAL;
using DMSystem.DAL.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace DMSystem.Tests.DAL
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
                LastModified = DateTime.Now,
                Author = "Test Author",
                FilePath = "testpath"
            };

            // Act
            var result = await repository.Add(document);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Document", result.Name);
            Assert.Equal("Test Author", result.Author);
            Assert.Single(context.Documents); // Verify document count in database
        }

        [Fact]
        public async Task GetAllDocuments_ShouldReturnAllDocuments()
        {
            // Arrange
            var context = GetInMemoryDbContext("GetAllDocuments_TestDatabase");
            var repository = new DocumentRepository(context);

            var documents = new List<Document>
            {
                new Document { Name = "Document 1", LastModified = DateTime.Now, Author = "Author 1", FilePath = "path1" },
                new Document { Name = "Document 2", LastModified = DateTime.Now, Author = "Author 2", FilePath = "path2" }
            };

            foreach (var doc in documents)
            {
                await repository.Add(doc);
            }

            // Act
            var result = await repository.GetAllDocumentsAsync();

            // Assert
            Assert.Equal(2, result.Count);
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
                LastModified = DateTime.Now,
                Author = "Test Author",
                FilePath = "testpath"
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
                LastModified = DateTime.Now,
                Author = "Initial Author",
                FilePath = "testpath"
            };

            var addedDocument = await repository.Add(document);

            // Act
            addedDocument.Author = "Updated Author";
            var updatedDocument = await repository.Update(addedDocument);

            // Assert
            Assert.Equal("Updated Author", updatedDocument.Author);
            Assert.Single(context.Documents.Where(d => d.Author == "Updated Author"));
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
                LastModified = DateTime.Now,
                Author = "Test Author",
                FilePath = "testpath"
            };

            var addedDocument = await repository.Add(document);

            // Act
            await repository.Remove(addedDocument);

            // Assert
            var result = await repository.GetByIdAsync(addedDocument.Id);
            Assert.Null(result); // Document should be null after removal
            Assert.Empty(context.Documents); // Verify database state
        }

        [Fact]
        public async Task AddDocument_ShouldThrowException_WhenDatabaseFails()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<DALContext>()
                .UseInMemoryDatabase("FailingDatabase")
                .Options;

            var context = new DALContext(options);
            var repository = new DocumentRepository(context);

            var document = new Document
            {
                Name = "Faulty Document",
                LastModified = DateTime.Now,
                Author = "Faulty Author",
                FilePath = "faultypath"
            };

            context.Dispose(); // Simulate a database failure

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await repository.Add(document));
        }
    }
}
