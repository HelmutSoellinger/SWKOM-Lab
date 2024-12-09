using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AutoMapper;
using DMSystem.Controllers;
using DMSystem.DAL;
using DMSystem.DAL.Models;
using DMSystem.DTOs;
using DMSystem.ElasticSearch;
using DMSystem.Messaging;
using DMSystem.Minio;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DMSystem.Tests
{
    public class DocumentControllerTests
    {
        private readonly Mock<IDocumentRepository> _mockRepository;
        private readonly Mock<ILogger<DocumentController>> _mockLogger;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IRabbitMQPublisher<OCRRequest>> _mockPublisher;
        private readonly Mock<IValidator<DocumentDTO>> _mockValidator;
        private readonly Mock<IFileStorageService> _mockFileStorageService;
        private readonly Mock<IElasticSearchService> _mockElasticSearchService;

        public DocumentControllerTests()
        {
            _mockRepository = new Mock<IDocumentRepository>();
            _mockLogger = new Mock<ILogger<DocumentController>>();
            _mockMapper = new Mock<IMapper>();
            _mockPublisher = new Mock<IRabbitMQPublisher<OCRRequest>>();
            _mockValidator = new Mock<IValidator<DocumentDTO>>();
            _mockFileStorageService = new Mock<IFileStorageService>();
            _mockElasticSearchService = new Mock<IElasticSearchService>();
        }

        [Fact]
        public async Task GetDocumentById_ReturnsDocument_WhenDocumentExists()
        {
            // Arrange
            var documentId = 1;
            var mockDocument = new Document { Id = documentId, Name = "Test Document" };
            _mockRepository.Setup(repo => repo.GetByIdAsync(documentId)).ReturnsAsync(mockDocument);
            _mockMapper.Setup(mapper => mapper.Map<DocumentDTO>(mockDocument))
                       .Returns(new DocumentDTO { Id = documentId, Name = "Test Document" });

            var controller = new DocumentController(
                _mockRepository.Object,
                _mockLogger.Object,
                _mockMapper.Object,
                _mockPublisher.Object,
                _mockValidator.Object,
                _mockFileStorageService.Object,
                _mockElasticSearchService.Object
            );

            // Act
            var result = await controller.GetDocumentById(documentId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedDocument = Assert.IsType<DocumentDTO>(okResult.Value);
            Assert.Equal(documentId, returnedDocument.Id);
        }

        [Fact]
        public async Task GetDocumentById_ReturnsNotFound_WhenDocumentDoesNotExist()
        {
            // Arrange
            var documentId = 1;
            _mockRepository.Setup(repo => repo.GetByIdAsync(documentId)).ReturnsAsync((Document?)null);

            var controller = new DocumentController(
                _mockRepository.Object,
                _mockLogger.Object,
                _mockMapper.Object,
                _mockPublisher.Object,
                _mockValidator.Object,
                _mockFileStorageService.Object,
                _mockElasticSearchService.Object
            );

            // Act
            var result = await controller.GetDocumentById(documentId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreateDocument_ReturnsCreatedDocument_WhenValidRequest()
        {
            // Arrange
            var documentDto = new DocumentDTO
            {
                Name = "Valid Name",
                Author = "Valid Author",
                LastModified = DateTime.Now
            };

            var pdfFile = new Mock<IFormFile>();
            pdfFile.Setup(f => f.FileName).Returns("test.pdf");
            pdfFile.Setup(f => f.Length).Returns(1024);

            var mockDocument = new Document
            {
                Id = 1,
                Name = "Valid Name",
                Author = "Valid Author",
                LastModified = DateTime.Now,
                FilePath = "path/to/file.pdf"
            };

            _mockRepository.Setup(repo => repo.Add(It.IsAny<Document>())).ReturnsAsync(mockDocument);
            _mockMapper.Setup(mapper => mapper.Map<Document>(documentDto)).Returns(mockDocument);
            _mockMapper.Setup(mapper => mapper.Map<DocumentDTO>(mockDocument))
                       .Returns(new DocumentDTO { Id = 1, Name = "Valid Name" });
            _mockValidator.Setup(validator => validator.ValidateAsync(documentDto, default))
                          .ReturnsAsync(new ValidationResult());

            var controller = new DocumentController(
                _mockRepository.Object,
                _mockLogger.Object,
                _mockMapper.Object,
                _mockPublisher.Object,
                _mockValidator.Object,
                _mockFileStorageService.Object,
                _mockElasticSearchService.Object
            );

            // Act
            var result = await controller.CreateDocument(documentDto, pdfFile.Object);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var returnedDocument = Assert.IsType<DocumentDTO>(createdResult.Value);
            Assert.Equal(mockDocument.Id, returnedDocument.Id);
        }

        [Fact]
        public async Task CreateDocument_ReturnsBadRequest_WhenFileIsInvalid()
        {
            // Arrange
            var documentDto = new DocumentDTO { Name = "Test Document" };
            IFormFile? pdfFile = null;

            var controller = new DocumentController(
                _mockRepository.Object,
                _mockLogger.Object,
                _mockMapper.Object,
                _mockPublisher.Object,
                _mockValidator.Object,
                _mockFileStorageService.Object,
                _mockElasticSearchService.Object
            );

            // Act
            var result = await controller.CreateDocument(documentDto, pdfFile);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errors = Assert.IsType<SerializableError>(badRequestResult.Value);
            Assert.Contains("pdfFile", errors.Keys);
        }

        [Fact]
        public async Task UploadDocumentFile_ReturnsOk_WhenSuccessful()
        {
            // Arrange
            var documentId = 1;

            var existingDocument = new Document
            {
                Id = documentId,
                Name = "Existing Document",
                Author = "Author",
                LastModified = DateTime.Now,
                FilePath = "path/to/old/file.pdf"
            };

            var pdfFile = new Mock<IFormFile>();
            pdfFile.Setup(f => f.FileName).Returns("test.pdf");
            pdfFile.Setup(f => f.Length).Returns(2048);

            _mockRepository.Setup(repo => repo.GetByIdAsync(documentId)).ReturnsAsync(existingDocument);
            _mockRepository.Setup(repo => repo.Update(It.IsAny<Document>())).ReturnsAsync(existingDocument);

            var controller = new DocumentController(
                _mockRepository.Object,
                _mockLogger.Object,
                _mockMapper.Object,
                _mockPublisher.Object,
                _mockValidator.Object,
                _mockFileStorageService.Object,
                _mockElasticSearchService.Object
            );

            // Act
            var result = await controller.UploadDocumentFile(documentId, pdfFile.Object);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;

            // Explicitly check for anonymous type with expected property
            Assert.NotNull(response);
            Assert.Contains("File uploaded and associated", response.ToString());
        }
    }
}
