using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AutoMapper;
using DMSystem.Controllers;
using DMSystem.DAL;
using DMSystem.DAL.Models;
using DMSystem.DTOs;
using DMSystem.Messaging;
using DMSystem.Minio;
using DMSystem.ElasticSearch;
using FluentValidation;
using FluentValidation.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace DMSystem.Tests
{
    public class DocumentControllerTests
    {
        private readonly Mock<IDocumentRepository> _documentRepositoryMock;
        private readonly Mock<ILogger<DocumentController>> _loggerMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IRabbitMQPublisher<OCRRequest>> _rabbitMqPublisherMock;
        private readonly Mock<IValidator<DocumentDTO>> _validatorMock;
        private readonly Mock<IFileStorageService> _fileStorageServiceMock;
        private readonly Mock<IElasticSearchService> _elasticSearchServiceMock;

        private readonly DocumentController _controller;

        public DocumentControllerTests()
        {
            _documentRepositoryMock = new Mock<IDocumentRepository>();
            _loggerMock = new Mock<ILogger<DocumentController>>();
            _mapperMock = new Mock<IMapper>();
            _rabbitMqPublisherMock = new Mock<IRabbitMQPublisher<OCRRequest>>();
            _validatorMock = new Mock<IValidator<DocumentDTO>>();
            _fileStorageServiceMock = new Mock<IFileStorageService>();
            _elasticSearchServiceMock = new Mock<IElasticSearchService>();

            _controller = new DocumentController(
                _documentRepositoryMock.Object,
                _loggerMock.Object,
                _mapperMock.Object,
                _rabbitMqPublisherMock.Object,
                _validatorMock.Object,
                _fileStorageServiceMock.Object,
                _elasticSearchServiceMock.Object
            );
        }

        [Fact]
        public async Task Get_WithNoName_ReturnsAllDocuments()
        {
            // Arrange
            var documents = new List<Document>
    {
        new Document { Id = 1, Name = "TestDoc1", FilePath = "file1.pdf" },
        new Document { Id = 2, Name = "TestDoc2", FilePath = "file2.pdf" }
    };

            _documentRepositoryMock.Setup(repo => repo.GetAllDocumentsAsync(null))
                .ReturnsAsync(documents);

            var documentDtos = new List<DocumentDTO>
    {
        new DocumentDTO { Id = 1, Name = "TestDoc1" },
        new DocumentDTO { Id = 2, Name = "TestDoc2" }
    };

            _mapperMock.Setup(m => m.Map<IEnumerable<DocumentDTO>>(documents)).Returns(documentDtos);

            // Act
            var actionResult = await _controller.Get(null); // ActionResult<IEnumerable<DocumentDTO>>

            // Assert
            var okResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(okResult);

            var returnedDocs = okResult.Value as IEnumerable<DocumentDTO>;
            Assert.NotNull(returnedDocs);
            Assert.Equal(2, returnedDocs.Count());
        }

        [Fact]
        public async Task Get_WithNameFilter_ReturnsFilteredDocuments()
        {
            var documents = new List<Document>
    {
        new Document { Id = 1, Name = "FilteredDoc", FilePath = "file1.pdf" }
    };

            _documentRepositoryMock.Setup(repo => repo.GetAllDocumentsAsync("FilteredDoc"))
                .ReturnsAsync(documents);

            var documentDtos = new List<DocumentDTO>
    {
        new DocumentDTO { Id = 1, Name = "FilteredDoc" }
    };

            _mapperMock.Setup(m => m.Map<IEnumerable<DocumentDTO>>(documents)).Returns(documentDtos);

            var actionResult = await _controller.Get("FilteredDoc"); // ActionResult<IEnumerable<DocumentDTO>>

            var okResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(okResult);

            var returnedDocs = okResult.Value as IEnumerable<DocumentDTO>;
            Assert.NotNull(returnedDocs);
            Assert.Single(returnedDocs);
            Assert.Equal("FilteredDoc", returnedDocs.First().Name);
        }

        [Fact]
        public async Task CreateDocument_WithNullFile_ReturnsBadRequest()
        {
            // Arrange
            var docDto = new DocumentDTO { Name = "TestDoc" };
            IFormFile pdfFile = null;

            // Act
            var result = await _controller.CreateDocument(docDto, pdfFile) as BadRequestObjectResult;

            // Assert
            Assert.NotNull(result);
            var errors = (result.Value as SerializableError);
            Assert.True(errors.ContainsKey("pdfFile"));
        }

        [Fact]
        public async Task CreateDocument_WithNonPdfFile_ReturnsBadRequest()
        {
            // Arrange
            var docDto = new DocumentDTO { Name = "TestDoc" };
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("notpdf.txt");
            fileMock.Setup(f => f.Length).Returns(100);
            fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));

            // Act
            var result = await _controller.CreateDocument(docDto, fileMock.Object) as BadRequestObjectResult;

            // Assert
            Assert.NotNull(result);
            var errors = result.Value as SerializableError;
            Assert.True(errors.ContainsKey("pdfFile"));
        }

        [Fact]
        public async Task CreateDocument_InvalidDto_ReturnsBadRequest()
        {
            // Arrange
            var docDto = new DocumentDTO { Name = "" }; // Assume empty name is invalid
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("file.pdf");
            fileMock.Setup(f => f.Length).Returns(100);
            fileMock.Setup(f => f.ContentType).Returns("application/pdf");
            fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));

            var validationResult = new ValidationResult(new List<ValidationFailure>
            {
                new ValidationFailure("Name", "Name cannot be empty.")
            });

            _validatorMock.Setup(v => v.ValidateAsync(docDto, default)).ReturnsAsync(validationResult);

            // Act
            var result = await _controller.CreateDocument(docDto, fileMock.Object) as BadRequestObjectResult;

            // Assert
            Assert.NotNull(result);
            var response = result.Value as dynamic;
            Assert.NotNull(response.errors);
        }

        [Fact]
        public async Task CreateDocument_WithValidData_ReturnsCreatedAt()
        {
            // Arrange
            var docDto = new DocumentDTO { Name = "TestDoc" };
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("file.pdf");
            fileMock.Setup(f => f.Length).Returns(100);
            fileMock.Setup(f => f.ContentType).Returns("application/pdf");
            fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));

            _validatorMock.Setup(v => v.ValidateAsync(docDto, default))
                .ReturnsAsync(new ValidationResult()); // valid

            var mappedDoc = new Document { Id = 123, Name = "TestDoc", FilePath = "uuid_file.pdf" };
            _mapperMock.Setup(m => m.Map<Document>(docDto)).Returns(mappedDoc);

            // If Add returns Task<Document>, we must return a Document via ReturnsAsync
            _documentRepositoryMock.Setup(r => r.Add(It.IsAny<Document>()))
                .Callback<Document>(d => d.Id = 123)
                .ReturnsAsync(mappedDoc);

            _fileStorageServiceMock.Setup(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<long>(), "application/pdf"))
                .Returns(Task.CompletedTask);

            _rabbitMqPublisherMock.Setup(p => p.PublishMessageAsync(It.IsAny<OCRRequest>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var docDtoResponse = new DocumentDTO { Id = 123, Name = "TestDoc" };
            _mapperMock.Setup(m => m.Map<DocumentDTO>(It.IsAny<Document>())).Returns(docDtoResponse);

            // Act
            var result = await _controller.CreateDocument(docDto, fileMock.Object) as CreatedAtActionResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal("GetDocumentById", result.ActionName);
            Assert.Equal(123, result.RouteValues["id"]);
            Assert.Equal(docDtoResponse, result.Value);
        }

        [Fact]
        public async Task GetDocumentById_NotFound_ReturnsNotFound()
        {
            int docId = 999;
            _documentRepositoryMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync((Document)null);

            var result = await _controller.GetDocumentById(docId) as NotFoundObjectResult;

            Assert.NotNull(result);
            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public async Task GetDocumentById_Found_ReturnsOk()
        {
            // Arrange
            int docId = 1;
            var doc = new Document { Id = docId, Name = "TestDoc" };
            var docDto = new DocumentDTO { Id = docId, Name = "TestDoc" };

            _documentRepositoryMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(doc);
            _mapperMock.Setup(m => m.Map<DocumentDTO>(doc)).Returns(docDto);

            // Act
            var result = await _controller.GetDocumentById(docId) as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            var returnedDoc = result.Value as DocumentDTO;
            Assert.Equal(docId, returnedDoc.Id);
        }

        [Fact]
        public async Task UploadDocumentFile_NoFile_ReturnsBadRequest()
        {
            // Arrange
            int docId = 1;
            IFormFile pdfFile = null;

            // Act
            var result = await _controller.UploadDocumentFile(docId, pdfFile) as BadRequestObjectResult;

            // Assert
            Assert.NotNull(result);
            var errors = (result.Value as SerializableError);
            Assert.True(errors.ContainsKey("pdfFile"));
        }

        [Fact]
        public async Task UploadDocumentFile_NonPdfFile_ReturnsBadRequest()
        {
            // Arrange
            int docId = 1;
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("image.png");
            fileMock.Setup(f => f.Length).Returns(100);

            // Act
            var result = await _controller.UploadDocumentFile(docId, fileMock.Object) as BadRequestObjectResult;

            // Assert
            Assert.NotNull(result);
            var errors = (result.Value as SerializableError);
            Assert.True(errors.ContainsKey("pdfFile"));
        }

        [Fact]
        public async Task UploadDocumentFile_DocNotFound_ReturnsNotFound()
        {
            // Arrange
            int docId = 999;
            _documentRepositoryMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync((Document)null);

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("file.pdf");
            fileMock.Setup(f => f.Length).Returns(100);
            fileMock.Setup(f => f.ContentType).Returns("application/pdf");
            fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));

            // Act
            var result = await _controller.UploadDocumentFile(docId, fileMock.Object) as NotFoundObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public async Task DeleteDocument_DocNotFound_ReturnsNotFound()
        {
            // Arrange
            int docId = 999;
            _documentRepositoryMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync((Document)null);

            // Act
            var result = await _controller.DeleteDocument(docId) as NotFoundResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public async Task CheckFileExists_FileExists_ReturnsOk()
        {
            // Arrange
            int docId = 1;
            var doc = new Document { Id = docId, FilePath = "file1.pdf" };
            _documentRepositoryMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(doc);
            _fileStorageServiceMock.Setup(s => s.FileExistsAsync(doc.FilePath)).ReturnsAsync(true);

            // Act
            var result = await _controller.CheckFileExists(docId) as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task CheckFileExists_FileDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            int docId = 1;
            var doc = new Document { Id = docId, FilePath = "file1.pdf" };
            _documentRepositoryMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(doc);
            _fileStorageServiceMock.Setup(s => s.FileExistsAsync(doc.FilePath)).ReturnsAsync(false);

            // Act
            var result = await _controller.CheckFileExists(docId) as NotFoundObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public async Task SearchDocuments_EmptyTerm_ReturnsBadRequest()
        {
            // Arrange
            string searchTerm = "";

            // Act
            var result = await _controller.SearchDocuments(searchTerm) as BadRequestObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task SearchDocuments_NoResults_ReturnsNotFound()
        {
            // Arrange
            string searchTerm = "test";
            _elasticSearchServiceMock.Setup(s => s.SearchDocumentsAsync(searchTerm))
                .ReturnsAsync(new List<SearchResult>()); // no results

            // Act
            var result = await _controller.SearchDocuments(searchTerm) as NotFoundObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public async Task SearchDocuments_Found_ReturnsOk()
        {
            // Arrange
            string searchTerm = "test";
            var results = new List<SearchResult>
            {
                new SearchResult { DocumentId = "1", MatchCount = 2 }
            };
            _elasticSearchServiceMock.Setup(s => s.SearchDocumentsAsync(searchTerm))
                .ReturnsAsync(results);

            // Act
            var result = await _controller.SearchDocuments(searchTerm) as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            var returned = result.Value as IEnumerable<SearchResult>;
            Assert.Single(returned);
            Assert.Equal("1", returned.First().DocumentId);
            Assert.Equal(2, returned.First().MatchCount);
        }
    }
}