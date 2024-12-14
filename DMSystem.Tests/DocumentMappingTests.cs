using AutoMapper;
using DMSystem.DAL.Models;
using DMSystem.Mappings;
using DMSystem.Contracts.DTOs;
using Xunit;

namespace DMSystem.Tests
{
    public class DocumentMappingTests
    {
        private readonly IMapper _mapper;

        public DocumentMappingTests()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<DocumentProfile>();
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
                FilePath = "sample.pdf"
            };

            // Act
            var documentDTO = _mapper.Map<DocumentDTO>(document);

            // Assert
            Assert.Equal(document.Id, documentDTO.Id);
            Assert.Equal(document.Name, documentDTO.Name);
            Assert.Equal(document.LastModified, documentDTO.LastModified);
            Assert.Equal(document.Author, documentDTO.Author);
            Assert.Equal(document.FilePath, documentDTO.FilePath);
        }

        [Fact]
        public void DocumentDTOTODocumentMapping_ValidMapping_IgnoresLastModified()
        {
            // Arrange
            var documentDTO = new DocumentDTO
            {
                Id = 1,
                Name = "Updated Document",
                LastModified = DateTime.Now,
                Author = "Jane Doe",
                FilePath = "updated_sample.pdf"
            };

            // Act
            var document = _mapper.Map<Document>(documentDTO);

            // Assert
            Assert.Equal(documentDTO.Id, document.Id);
            Assert.Equal(documentDTO.Name, document.Name);
            Assert.NotEqual(documentDTO.LastModified, document.LastModified); // LastModified is ignored in mapping
            Assert.Equal(documentDTO.Author, document.Author);
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
            config.AssertConfigurationIsValid(); // Validates AutoMapper configuration
        }
    }
}
