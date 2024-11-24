using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DMSystem.Controllers;
using DMSystem.DAL.Models;
using DMSystem.DAL;
using DMSystem.DTOs;
using DMSystem.Messaging;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DMSystem.Tests
{
    public class DocumentControllerTests
    {
        private readonly Mock<IDocumentRepository> _mockDocumentRepository;
        private readonly Mock<ILogger<DocumentController>> _mockLogger;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IRabbitMQPublisher<OCRRequest>> _mockRabbitMqPublisher;
        private readonly Mock<IValidator<DocumentDTO>> _mockValidator;
        private readonly DocumentController _controller;

        public DocumentControllerTests()
        {
            _mockDocumentRepository = new Mock<IDocumentRepository>();
            _mockLogger = new Mock<ILogger<DocumentController>>();
            _mockMapper = new Mock<IMapper>();
            _mockRabbitMqPublisher = new Mock<IRabbitMQPublisher<OCRRequest>>();
            _mockValidator = new Mock<IValidator<DocumentDTO>>();

            _controller = new DocumentController(
                _mockDocumentRepository.Object,
                _mockLogger.Object,
                _mockMapper.Object,
                _mockRabbitMqPublisher.Object,
                _mockValidator.Object
            );
        }

        [Fact]
        public async Task Get_ReturnsListOfDocuments_WhenDocumentsExist()
        {
            // Arrange
            var documents = new List<Document> { new Document { Id = 1, Name = "Doc1" }, new Document { Id = 2, Name = "Doc2" } };
            var documentDTOs = documents.Select(doc => new DocumentDTO { Id = doc.Id, Name = doc.Name }).ToList();

            _mockDocumentRepository.Setup(repo => repo.GetAllDocumentsAsync(null)).ReturnsAsync(documents);
            _mockMapper.Setup(mapper => mapper.Map<IEnumerable<DocumentDTO>>(documents)).Returns(documentDTOs);

            // Act
            var result = await _controller.Get(null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(documentDTOs, okResult.Value);
        }

        [Fact]
        public async Task PostDocument_ReturnsCreatedDocument_WhenValidInput()
        {
            // Arrange
            var createDocument = new DocumentDTO { Id = 1, Name = "New Document" };
            var pdfFile = new Mock<IFormFile>();
            pdfFile.Setup(f => f.Length).Returns(1000);
            pdfFile.Setup(f => f.FileName).Returns("test.pdf");

            var validationResult = new ValidationResult();
            _mockValidator.Setup(validator => validator.ValidateAsync(createDocument, default)).ReturnsAsync(validationResult);

            var mappedDocument = new Document { Id = 1, Name = createDocument.Name, FilePath = "UploadedFiles/test.pdf" };
            _mockMapper.Setup(mapper => mapper.Map<Document>(createDocument)).Returns(mappedDocument);

            _mockDocumentRepository.Setup(repo => repo.Add(mappedDocument)).ReturnsAsync(mappedDocument);
            _mockRabbitMqPublisher.Setup(pub => pub.PublishMessageAsync(It.IsAny<OCRRequest>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            _mockMapper.Setup(mapper => mapper.Map<DocumentDTO>(mappedDocument)).Returns(createDocument);

            // Act
            var result = await _controller.PostDocument(createDocument, pdfFile.Object);

            // Assert
            var createdAtResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(createDocument, createdAtResult.Value);
        }

        [Fact]
        public async Task PostDocument_ReturnsBadRequest_WhenAuthorIsMissing()
        {
            // Arrange
            var createDocument = new DocumentDTO { Name = "Test Document" }; // Missing Author
            var pdfFile = new Mock<IFormFile>();
            var validationErrors = new List<ValidationFailure>
    {
        new ValidationFailure("Author", "Author is required")
    };
            var validationResult = new ValidationResult(validationErrors);

            _mockValidator.Setup(validator => validator.ValidateAsync(createDocument, default)).ReturnsAsync(validationResult);

            // Act
            var result = await _controller.PostDocument(createDocument, pdfFile.Object);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);

            // Assert the structure of the errors
            Assert.NotNull(badRequestResult.Value);

            if (badRequestResult.Value is IDictionary<string, object> errorsDictionary)
            {
                var errorList = errorsDictionary["errors"] as IEnumerable<dynamic>;
                Assert.NotNull(errorList);
                Assert.Single(errorList);

                var error = errorList.First();
                Assert.Equal("Author", (string)error.Property);
                Assert.Equal("Author is required", (string)error.Message);
            }
            else
            {
                Assert.True(false, "The response does not contain a valid errors structure.");
            }
        }



        [Fact]
        public async Task PutDocument_ReturnsNoContent_WhenUpdateIsSuccessful()
        {
            // Arrange
            var documentId = 1;
            var documentDTO = new DocumentDTO { Id = documentId, Name = "Updated Document" };
            var existingDocument = new Document { Id = documentId, Name = "Old Document" };

            _mockDocumentRepository.Setup(repo => repo.GetByIdAsync(documentId)).ReturnsAsync(existingDocument);
            _mockDocumentRepository.Setup(repo => repo.Update(existingDocument)).ReturnsAsync(existingDocument);

            // Act
            var result = await _controller.PutDocument(documentId, documentDTO);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteDocument_ReturnsNoContent_WhenDeleteIsSuccessful()
        {
            // Arrange
            var documentId = 1;
            var document = new Document { Id = documentId, FilePath = "UploadedFiles/test.pdf" };

            _mockDocumentRepository.Setup(repo => repo.GetByIdAsync(documentId)).ReturnsAsync(document);
            _mockDocumentRepository.Setup(repo => repo.Remove(document)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteDocument(documentId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteDocument_ReturnsNotFound_WhenDocumentDoesNotExist()
        {
            // Arrange
            var documentId = 1;
            _mockDocumentRepository.Setup(repo => repo.GetByIdAsync(documentId)).ReturnsAsync((Document)null);

            // Act
            var result = await _controller.DeleteDocument(documentId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
