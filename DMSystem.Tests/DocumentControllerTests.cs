using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AutoMapper;
using DMSystem.Controllers;
using DMSystem.DAL;
using DMSystem.DAL.Models;
using DMSystem.Contracts.DTOs;
using DMSystem.Minio;
using DMSystem.ElasticSearch;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DMSystem.Contracts;

namespace DMSystem.Tests
{
    public class DocumentControllerTests
    {
        private readonly Mock<IDocumentRepository> _documentRepositoryMock;
        private readonly Mock<ILogger<DocumentController>> _loggerMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IRabbitMQService> _rabbitMqServiceMock;
        private readonly Mock<IValidator<DocumentDTO>> _validatorMock;
        private readonly Mock<IFileStorageService> _fileStorageServiceMock;
        private readonly Mock<IElasticSearchService> _elasticSearchServiceMock;

        private readonly DocumentController _controller;

        public DocumentControllerTests()
        {
            _documentRepositoryMock = new Mock<IDocumentRepository>();
            _loggerMock = new Mock<ILogger<DocumentController>>();
            _mapperMock = new Mock<IMapper>();
            _rabbitMqServiceMock = new Mock<IRabbitMQService>(); // Changed from IRabbitMQPublisher
            _validatorMock = new Mock<IValidator<DocumentDTO>>();
            _fileStorageServiceMock = new Mock<IFileStorageService>();
            _elasticSearchServiceMock = new Mock<IElasticSearchService>();

            // Since we no longer have RabbitMQSetting as constructor arg directly,
            // you may need to provide a mock IOptions<RabbitMQSettings> as well.
            // For simplicity, let's assume OCRQueue name is "ocrQueue":
            var rabbitMqSettings = Microsoft.Extensions.Options.Options.Create(new RabbitMQSettings
            {
                Queues = new Dictionary<string, string>
                {
                    { "OcrQueue", "ocrQueue" }
                }
            });

            _controller = new DocumentController(
                _documentRepositoryMock.Object,
                _loggerMock.Object,
                _mapperMock.Object,
                _rabbitMqServiceMock.Object,
                _validatorMock.Object,
                _fileStorageServiceMock.Object,
                _elasticSearchServiceMock.Object,
                rabbitMqSettings
            );
        }

        [Fact]
        public async Task Get_WithNoName_ReturnsAllDocuments()
        {
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

            var actionResult = await _controller.Get(null);

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

            var actionResult = await _controller.Get("FilteredDoc");

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
            var docDto = new DocumentDTO { Name = "TestDoc" };
            IFormFile pdfFile = null;

            var result = await _controller.CreateDocument(docDto, pdfFile) as BadRequestObjectResult;

            Assert.NotNull(result);
            var errors = (result.Value as SerializableError);
            Assert.True(errors.ContainsKey("pdfFile"));
        }

        [Fact]
        public async Task CreateDocument_WithNonPdfFile_ReturnsBadRequest()
        {
            var docDto = new DocumentDTO { Name = "TestDoc" };
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("notpdf.txt");
            fileMock.Setup(f => f.Length).Returns(100);
            fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));

            var result = await _controller.CreateDocument(docDto, fileMock.Object) as BadRequestObjectResult;

            Assert.NotNull(result);
            var errors = result.Value as SerializableError;
            Assert.True(errors.ContainsKey("pdfFile"));
        }

        [Fact]
        public async Task CreateDocument_InvalidDto_ReturnsBadRequest()
        {
            var docDto = new DocumentDTO { Name = "" };
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

            var result = await _controller.CreateDocument(docDto, fileMock.Object) as BadRequestObjectResult;

            Assert.NotNull(result);
            var response = result.Value as dynamic;
            Assert.NotNull(response.errors);
        }

        [Fact]
        public async Task CreateDocument_WithValidData_ReturnsCreatedAt()
        {
            var docDto = new DocumentDTO { Name = "TestDoc" };
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("file.pdf");
            fileMock.Setup(f => f.Length).Returns(100);
            fileMock.Setup(f => f.ContentType).Returns("application/pdf");
            fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));

            _validatorMock.Setup(v => v.ValidateAsync(docDto, default))
                .ReturnsAsync(new ValidationResult());

            var mappedDoc = new Document { Id = 123, Name = "TestDoc", FilePath = "uuid_file.pdf" };
            _mapperMock.Setup(m => m.Map<Document>(docDto)).Returns(mappedDoc);

            _documentRepositoryMock.Setup(r => r.Add(It.IsAny<Document>()))
                .Callback<Document>(d => d.Id = 123)
                .ReturnsAsync(mappedDoc);

            _fileStorageServiceMock.Setup(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<long>(), "application/pdf"))
                .Returns(Task.CompletedTask);

            // Since we now use IRabbitMQService, setup that instead of IRabbitMQPublisher
            _rabbitMqServiceMock.Setup(p => p.PublishMessageAsync(It.IsAny<OCRRequest>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var docDtoResponse = new DocumentDTO { Id = 123, Name = "TestDoc" };
            _mapperMock.Setup(m => m.Map<DocumentDTO>(It.IsAny<Document>())).Returns(docDtoResponse);

            var result = await _controller.CreateDocument(docDto, fileMock.Object) as CreatedAtActionResult;

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
            int docId = 1;
            var doc = new Document { Id = docId, Name = "TestDoc" };
            var docDto = new DocumentDTO { Id = docId, Name = "TestDoc" };

            _documentRepositoryMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(doc);
            _mapperMock.Setup(m => m.Map<DocumentDTO>(doc)).Returns(docDto);

            var result = await _controller.GetDocumentById(docId) as OkObjectResult;

            Assert.NotNull(result);
            var returnedDoc = result.Value as DocumentDTO;
            Assert.Equal(docId, returnedDoc.Id);
        }

        [Fact]
        public async Task UploadDocumentFile_NoFile_ReturnsBadRequest()
        {
            int docId = 1;
            IFormFile pdfFile = null;

            var result = await _controller.UploadDocumentFile(docId, pdfFile) as BadRequestObjectResult;

            Assert.NotNull(result);
            var errors = (result.Value as SerializableError);
            Assert.True(errors.ContainsKey("pdfFile"));
        }

        [Fact]
        public async Task UploadDocumentFile_NonPdfFile_ReturnsBadRequest()
        {
            int docId = 1;
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("image.png");
            fileMock.Setup(f => f.Length).Returns(100);

            var result = await _controller.UploadDocumentFile(docId, fileMock.Object) as BadRequestObjectResult;

            Assert.NotNull(result);
            var errors = (result.Value as SerializableError);
            Assert.True(errors.ContainsKey("pdfFile"));
        }

        [Fact]
        public async Task UploadDocumentFile_DocNotFound_ReturnsNotFound()
        {
            int docId = 999;
            _documentRepositoryMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync((Document)null);

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("file.pdf");
            fileMock.Setup(f => f.Length).Returns(100);
            fileMock.Setup(f => f.ContentType).Returns("application/pdf");
            fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[100]));

            var result = await _controller.UploadDocumentFile(docId, fileMock.Object) as NotFoundObjectResult;

            Assert.NotNull(result);
            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public async Task DeleteDocument_DocNotFound_ReturnsNotFound()
        {
            int docId = 999;
            _documentRepositoryMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync((Document)null);

            var result = await _controller.DeleteDocument(docId) as NotFoundResult;

            Assert.NotNull(result);
            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public async Task CheckFileExists_FileExists_ReturnsOk()
        {
            int docId = 1;
            var doc = new Document { Id = docId, FilePath = "file1.pdf" };
            _documentRepositoryMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(doc);
            _fileStorageServiceMock.Setup(s => s.FileExistsAsync(doc.FilePath)).ReturnsAsync(true);

            var result = await _controller.CheckFileExists(docId) as OkObjectResult;

            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task CheckFileExists_FileDoesNotExist_ReturnsNotFound()
        {
            int docId = 1;
            var doc = new Document { Id = docId, FilePath = "file1.pdf" };
            _documentRepositoryMock.Setup(r => r.GetByIdAsync(docId)).ReturnsAsync(doc);
            _fileStorageServiceMock.Setup(s => s.FileExistsAsync(doc.FilePath)).ReturnsAsync(false);

            var result = await _controller.CheckFileExists(docId) as NotFoundObjectResult;

            Assert.NotNull(result);
            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public async Task SearchDocuments_EmptyTerm_ReturnsBadRequest()
        {
            string searchTerm = "";

            var result = await _controller.SearchDocuments(searchTerm) as BadRequestObjectResult;

            Assert.NotNull(result);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task SearchDocuments_NoResults_ReturnsNotFound()
        {
            string searchTerm = "test";
            // Now we return IEnumerable<OCRResult>, so return empty OCRResult list
            _elasticSearchServiceMock.Setup(s => s.SearchDocumentsAsync(searchTerm))
                .ReturnsAsync(new List<OCRResult>()); // no results

            var result = await _controller.SearchDocuments(searchTerm) as NotFoundObjectResult;

            Assert.NotNull(result);
            Assert.Equal(404, result.StatusCode);
        }

        [Fact]
        public async Task SearchDocuments_Found_ReturnsOk()
        {
            string searchTerm = "test";
            // Return a list of OCRResults now
            var results = new List<OCRResult>
            {
                new OCRResult
                {
                    Document = new DocumentDTO { Id = 1, Name = "TestDoc", Author = "Author", LastModified = DateTime.UtcNow, FilePath = "file.pdf"},
                    OcrText = "Text containing test"
                }
            };
            _elasticSearchServiceMock.Setup(s => s.SearchDocumentsAsync(searchTerm))
                .ReturnsAsync(results);

            var result = await _controller.SearchDocuments(searchTerm) as OkObjectResult;

            Assert.NotNull(result);
            var returned = result.Value as IEnumerable<OCRResult>;
            Assert.Single(returned);
            Assert.Equal(1, returned.First().Document.Id);
            Assert.Contains("test", returned.First().OcrText);
        }
    }
}
