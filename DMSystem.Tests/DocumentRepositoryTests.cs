using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DMSystem.DAL;
using DMSystem.DAL.Models;

public class DocumentRepositoryTests
{
    private readonly DbContextOptions<DALContext> _dbContextOptions;

    public DocumentRepositoryTests()
    {
        // Set up in-memory database for testing
        _dbContextOptions = new DbContextOptionsBuilder<DALContext>()
            .UseInMemoryDatabase(databaseName: "TestDatabase")
            .Options;
    }

    [Fact]
    public async Task Add_ShouldAddDocumentToDatabase()
    {
        // Arrange
        using var context = new DALContext(_dbContextOptions);
        var repository = new DocumentRepository(context);
        var newDocument = new Document
        {
            Name = "Test Document",
            Author = "John Doe",
            LastModified = DateTime.UtcNow,
            Description = "A test document",
            FilePath = "/uploadedfiles/test.pdf"
        };

        // Act
        var result = await repository.Add(newDocument);

        // Assert
        var documentInDb = await context.Documents.FindAsync(result.Id);
        Assert.NotNull(documentInDb);
        Assert.Equal("Test Document", documentInDb.Name);
    }

    [Fact]
    public async Task Update_ShouldUpdateDocumentInDatabase()
    {
        // Arrange
        using var context = new DALContext(_dbContextOptions);
        var repository = new DocumentRepository(context);
        var document = new Document
        {
            Name = "Original Document",
            Author = "John Doe",
            LastModified = DateTime.UtcNow,
            Description = "A test document",
            FilePath = "/uploadedfiles/original.pdf"
        };

        await repository.Add(document);

        // Act
        document.Name = "Updated Document";
        document.Description = "Updated description";
        var updatedDocument = await repository.Update(document);

        // Assert
        var documentInDb = await context.Documents.FindAsync(updatedDocument.Id);
        Assert.NotNull(documentInDb);
        Assert.Equal("Updated Document", documentInDb.Name);
        Assert.Equal("Updated description", documentInDb.Description);
    }

    [Fact]
    public async Task Remove_ShouldDeleteDocumentFromDatabase()
    {
        // Arrange
        using var context = new DALContext(_dbContextOptions);
        var repository = new DocumentRepository(context);
        var document = new Document
        {
            Name = "Document to Delete",
            Author = "John Doe",
            LastModified = DateTime.UtcNow,
            Description = "A test document",
            FilePath = "/uploadedfiles/delete.pdf"
        };

        await repository.Add(document);

        // Act
        await repository.Remove(document);

        // Assert
        var documentInDb = await context.Documents.FindAsync(document.Id);
        Assert.Null(documentInDb);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnDocumentById()
    {
        // Arrange
        using var context = new DALContext(_dbContextOptions);
        var repository = new DocumentRepository(context);
        var document = new Document
        {
            Name = "Find By ID",
            Author = "John Doe",
            LastModified = DateTime.UtcNow,
            Description = "A test document",
            FilePath = "/uploadedfiles/findbyid.pdf"
        };

        var addedDocument = await repository.Add(document);

        // Act
        var result = await repository.GetByIdAsync(addedDocument.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Find By ID", result.Name);
    }

    [Fact]
    public async Task GetAllDocumentsAsync_ShouldReturnAllDocuments()
    {
        // Arrange
        using var context = new DALContext(_dbContextOptions);
        context.Database.EnsureDeleted(); // Clear the database
        context.Database.EnsureCreated(); // Recreate the schema
        var repository = new DocumentRepository(context);

        await repository.Add(new Document
        {
            Name = "Document 1",
            Author = "Author 1",
            LastModified = DateTime.UtcNow,
            FilePath = "/uploadedfiles/doc1.pdf"
        });

        await repository.Add(new Document
        {
            Name = "Document 2",
            Author = "Author 2",
            LastModified = DateTime.UtcNow,
            FilePath = "/uploadedfiles/doc2.pdf"
        });

        // Act
        var result = await repository.GetAllDocumentsAsync(null);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetDocumentsByName_ShouldReturnMatchingDocuments()
    {
        // Arrange
        using var context = new DALContext(_dbContextOptions);
        context.Database.EnsureDeleted(); // Clear the database
        context.Database.EnsureCreated(); // Recreate the schema
        var repository = new DocumentRepository(context);

        await repository.Add(new Document
        {
            Name = "Test Document 1",
            Author = "Author 1",
            LastModified = DateTime.UtcNow,
            FilePath = "/uploadedfiles/test1.pdf"
        });

        await repository.Add(new Document
        {
            Name = "Another Document",
            Author = "Author 2",
            LastModified = DateTime.UtcNow,
            FilePath = "/uploadedfiles/test2.pdf"
        });

        // Act
        var result = await repository.GetDocumentsByName("Test");

        // Assert
        Assert.Single(result);
        Assert.Equal("Test Document 1", result.First().Name);
    }
}
