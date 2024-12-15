using Xunit;
using Moq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using DMSystem.Controllers;
using DMSystem.DAL;
using DMSystem.DAL.Models;
using AutoMapper;
using DMSystem.Contracts.DTOs;
using FluentValidation;
using DMSystem.Minio;
using DMSystem.ElasticSearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DMSystem.Contracts;
using Microsoft.AspNetCore.Http;

namespace DMSystem.Tests.DMSystem
{
    public class DocumentControllerTests
    {
        private readonly Mock<IDocumentRepository> _mockRepository;
        private readonly Mock<ILogger<DocumentController>> _mockLogger;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IRabbitMQService> _mockRabbitMqService;
        private readonly Mock<IValidator<DocumentDTO>> _mockValidator;
        private readonly Mock<IMinioFileStorageService> _mockFileStorageService;
        private readonly Mock<IElasticSearchService> _mockElasticSearchService;
        private readonly Mock<IOptions<RabbitMQSettings>> _mockRabbitMqSettings;

        private readonly DocumentController _controller;

        public DocumentControllerTests()
        {
            _mockRepository = new Mock<IDocumentRepository>();
            _mockLogger = new Mock<ILogger<DocumentController>>();
            _mockMapper = new Mock<IMapper>();
            _mockRabbitMqService = new Mock<IRabbitMQService>();
            _mockValidator = new Mock<IValidator<DocumentDTO>>();
            _mockFileStorageService = new Mock<IMinioFileStorageService>();
            _mockElasticSearchService = new Mock<IElasticSearchService>();
            _mockRabbitMqSettings = new Mock<IOptions<RabbitMQSettings>>();

            _mockRabbitMqSettings.Setup(r => r.Value).Returns(new RabbitMQSettings
            {
                Queues = new Dictionary<string, string>
                {
                    { "OcrQueue", "ocr_queue" }
                }
            });

            _controller = new DocumentController(
                _mockRepository.Object,
                _mockLogger.Object,
                _mockMapper.Object,
                _mockRabbitMqService.Object,
                _mockValidator.Object,
                _mockFileStorageService.Object,
                _mockElasticSearchService.Object,
                _mockRabbitMqSettings.Object
            );
        }

        [Fact]
        public async Task Get_ShouldReturnAllDocuments()
        {
            var mockDocuments = new List<Document>
            {
                new Document { Id = 1, Name = "TestDoc1" },
                new Document { Id = 2, Name = "TestDoc2" }
            };
            _mockRepository.Setup(r => r.GetAllDocumentsAsync()).ReturnsAsync(mockDocuments);
            _mockMapper.Setup(m => m.Map<IEnumerable<DocumentDTO>>(mockDocuments))
                .Returns(new List<DocumentDTO>
                {
                    new DocumentDTO { Id = 1, Name = "TestDoc1" },
                    new DocumentDTO { Id = 2, Name = "TestDoc2" }
                });

            var result = await _controller.Get();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var documents = Assert.IsType<List<DocumentDTO>>(okResult.Value);
            Assert.Equal(2, documents.Count);
        }

        [Fact]
        public async Task GetDocumentById_ShouldReturnDocument_WhenExists()
        {
            var mockDocument = new Document { Id = 1, Name = "TestDoc" };
            _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(mockDocument);
            _mockMapper.Setup(m => m.Map<DocumentDTO>(mockDocument))
                .Returns(new DocumentDTO { Id = 1, Name = "TestDoc" });

            var result = await _controller.GetDocumentById(1);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var document = Assert.IsType<DocumentDTO>(okResult.Value);
            Assert.Equal(1, document.Id);
            Assert.Equal("TestDoc", document.Name);
        }

        [Fact]
        public async Task GetDocumentById_ShouldReturnNotFound_WhenDoesNotExist()
        {
            _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Document)null);

            var result = await _controller.GetDocumentById(1);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreateDocument_ShouldReturnCreatedDocument()
        {
            var mockDocument = new Document { Id = 1, Name = "TestDoc" };
            var documentDto = new DocumentDTO { Id = 1, Name = "TestDoc" };
            var mockFile = new Mock<IFormFile>();

            _mockValidator.Setup(v => v.ValidateAsync(documentDto, default))
                .ReturnsAsync(new FluentValidation.Results.ValidationResult());
            _mockMapper.Setup(m => m.Map<Document>(documentDto)).Returns(mockDocument);
            _mockRepository.Setup(r => r.Add(It.IsAny<Document>()))
                .Returns(Task.FromResult(mockDocument));
            _mockMapper.Setup(m => m.Map<DocumentDTO>(mockDocument)).Returns(documentDto);

            var result = await _controller.CreateDocument(documentDto, mockFile.Object);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var returnedDocument = Assert.IsType<DocumentDTO>(createdResult.Value);
            Assert.Equal("TestDoc", returnedDocument.Name);
        }

        [Fact]
        public async Task CreateDocument_ShouldHandleFileUploadErrors()
        {
            var documentDto = new DocumentDTO { Id = 1, Name = "TestDoc" };
            var mockFile = new Mock<IFormFile>();

            _mockValidator.Setup(v => v.ValidateAsync(documentDto, default))
                .ReturnsAsync(new FluentValidation.Results.ValidationResult());
            _mockFileStorageService.Setup(f => f.UploadFileAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("File upload failed"));

            var result = await _controller.CreateDocument(documentDto, mockFile.Object);

            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            Assert.Contains("Error uploading file", statusCodeResult.Value.ToString());
        }

        [Fact]
        public async Task CreateDocument_ShouldHandleRabbitMqErrors()
        {
            var mockDocument = new Document { Id = 1, Name = "TestDoc" };
            var documentDto = new DocumentDTO { Id = 1, Name = "TestDoc" };
            var mockFile = new Mock<IFormFile>();

            _mockValidator.Setup(v => v.ValidateAsync(documentDto, default))
                .ReturnsAsync(new FluentValidation.Results.ValidationResult());
            _mockMapper.Setup(m => m.Map<Document>(documentDto)).Returns(mockDocument);
            _mockRepository.Setup(r => r.Add(It.IsAny<Document>()))
                .Returns(Task.FromResult(mockDocument));
            _mockMapper.Setup(m => m.Map<DocumentDTO>(mockDocument)).Returns(documentDto);
            _mockRabbitMqService.Setup(r => r.PublishMessageAsync(It.IsAny<OCRRequest>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("RabbitMQ error"));

            var result = await _controller.CreateDocument(documentDto, mockFile.Object);

            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            Assert.Contains("Error sending OCR request", statusCodeResult.Value.ToString());
        }

        [Fact]
        public async Task DeleteDocument_ShouldReturnNoContent_WhenDocumentExists()
        {
            var mockDocument = new Document { Id = 1, Name = "TestDoc" };
            _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(mockDocument);
            _mockRepository.Setup(r => r.Remove(mockDocument)).Returns(Task.CompletedTask);

            var result = await _controller.DeleteDocument(1);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteDocument_ShouldHandleFileDeletionErrors()
        {
            var mockDocument = new Document { Id = 1, Name = "TestDoc", FilePath = "file.pdf" };
            _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(mockDocument);
            _mockFileStorageService.Setup(f => f.DeleteFileAsync(mockDocument.FilePath))
                .ThrowsAsync(new Exception("File deletion failed"));

            var result = await _controller.DeleteDocument(1);

            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            Assert.Contains("Error deleting file", statusCodeResult.Value.ToString());
        }

        [Fact]
        public async Task DeleteDocument_ShouldReturnNotFound_WhenDocumentDoesNotExist()
        {
            _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Document)null);

            var result = await _controller.DeleteDocument(1);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task SearchDocuments_ShouldReturnResults_WhenMatchesFound()
        {
            var searchResults = new List<OCRResult>
            {
                new OCRResult
                {
                    Document = new DocumentDTO { Id = 1, Name = "TestDoc" },
                    OcrText = "Sample text"
                }
            };
            _mockElasticSearchService.Setup(e => e.SearchDocumentsAsync("Test"))
                .ReturnsAsync(searchResults);

            var result = await _controller.SearchDocuments("Test");

            var okResult = Assert.IsType<OkObjectResult>(result);
            var results = Assert.IsType<List<OCRResult>>(okResult.Value);
            Assert.Single(results);
        }

        [Fact]
        public async Task SearchDocuments_ShouldReturnNotFound_WhenNoMatchesFound()
        {
            _mockElasticSearchService.Setup(e => e.SearchDocumentsAsync("Test"))
                .ReturnsAsync(new List<OCRResult>());

            var result = await _controller.SearchDocuments("Test");

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task SearchDocuments_ShouldReturnBadRequest_WhenSearchTermIsEmpty()
        {
            var result = await _controller.SearchDocuments("");

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
