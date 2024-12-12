using AutoMapper;
using DMSystem.DAL.Models;
using DMSystem.Controllers;
using DMSystem.Mappings;
using DMSystem.DTOs;
using Xunit;
using System.ComponentModel.DataAnnotations;

namespace DMSystem.Tests
{
    public class DocumentMappingTests
    {
        private readonly IMapper _mapper;

        public DocumentMappingTests()
        {
            // Initialize AutoMapper with the profile configuration
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<DocumentProfile>();  // Use the DocumentProfile for mapping
            });

            _mapper = config.CreateMapper();
        }

        [Fact]
        public void DocumentToDocumentDTOMapping_ValidMapping()
        {
            // Arrange
            var document = new Document
            {
                Id = 1,
                Name = "Sample Document",
                LastModified = DateTime.Now,
                Author = "John Doe",
                Description = "A sample document",
            };

            // Act
            var documentDTO = _mapper.Map<DocumentDTO>(document);

            // Assert
            Assert.Equal(document.Id, documentDTO.Id);
            Assert.Equal(document.Name, documentDTO.Name);
            Assert.Equal(document.LastModified, documentDTO.LastModified);
            Assert.Equal(document.Author, documentDTO.Author);
            Assert.Equal(document.Description, documentDTO.Description);
        }

        [Fact]
        public void DocumentDTOTODocumentMapping_ValidMapping_IgnoresContent()
        {
            // Arrange
            var documentDTO = new DocumentDTO
            {
                Id = 1,
                Name = "Updated Document",
                LastModified = DateTime.Now,
                Author = "Jane Doe",
                Description = "An updated sample document"
            };

            // Act
            var document = _mapper.Map<Document>(documentDTO);

            // Assert
            Assert.Equal(documentDTO.Id, document.Id);
            Assert.Equal(documentDTO.Name, document.Name);
            Assert.Equal(documentDTO.LastModified, document.LastModified);
            Assert.Equal(documentDTO.Author, document.Author);
            Assert.Equal(documentDTO.Description, document.Description);

        }

        [Fact]
        public void AutoMapperConfiguration_IsValid()
        {
            // Arrange & Act
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<DocumentProfile>();
            });

            // Assert
            config.AssertConfigurationIsValid();  // This checks if all mappings are valid
        }


        [Fact]
        public void DocumentToDocumentDTOMapping_MissingName_ShouldFail()
        {
            // Arrange
            var document = new Document
            {
                Id = 1,
                Name = null,  // Name is required, so this should fail
                LastModified = DateTime.Now,
                Author = "John Doe",
                Description = "A sample document",
            };

            // Manually validate the document object
            var validationContext = new ValidationContext(document, serviceProvider: null, items: null);
            var validationResults = new List<ValidationResult>();

            // Assert that the validation fails because Name is null
            bool isValid = Validator.TryValidateObject(document, validationContext, validationResults, validateAllProperties: true);

            Assert.False(isValid);  // This will fail because Name is null
            Assert.Contains(validationResults, v => v.MemberNames.Contains("Name"));
        }
    }
}
