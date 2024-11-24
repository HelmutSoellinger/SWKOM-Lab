using DMSystem.DTOs;
using FluentValidation.TestHelper;
using Xunit;

namespace DMSystem.Tests
{
    public class DocumentDTOTests
    {
        private readonly DocumentDTOValidator _validator;

        public DocumentDTOTests()
        {
            _validator = new DocumentDTOValidator();
        }

        [Fact]
        public void Validate_Should_Have_No_Errors_For_Valid_Document()
        {
            // Arrange
            var document = new DocumentDTO
            {
                Id = 1,
                Name = "Valid Document",
                Author = "John Doe",
                Description = "This is a valid document.",
                LastModified = DateTime.Now
            };

            // Act & Assert
            var result = _validator.TestValidate(document);

            result.ShouldNotHaveValidationErrorFor(doc => doc.Id);
            result.ShouldNotHaveValidationErrorFor(doc => doc.Name);
            result.ShouldNotHaveValidationErrorFor(doc => doc.Author);
            result.ShouldNotHaveValidationErrorFor(doc => doc.Description);
            result.ShouldNotHaveValidationErrorFor(doc => doc.LastModified);
        }

        [Fact]
        public void Validate_Should_Have_Error_For_Missing_Name()
        {
            // Arrange
            var document = new DocumentDTO
            {
                Id = 1,
                Name = string.Empty,
                Author = "John Doe",
                Description = "This is a valid document.",
                LastModified = DateTime.Now
            };

            // Act & Assert
            var result = _validator.TestValidate(document);

            result.ShouldHaveValidationErrorFor(doc => doc.Name);
        }

        [Fact]
        public void Validate_Should_Have_Error_For_Name_Too_Long()
        {
            // Arrange
            var document = new DocumentDTO
            {
                Id = 1,
                Name = new string('A', 101),
                Author = "John Doe",
                Description = "This is a valid document.",
                LastModified = DateTime.Now
            };

            // Act & Assert
            var result = _validator.TestValidate(document);

            result.ShouldHaveValidationErrorFor(doc => doc.Name);
        }

        [Fact]
        public void Validate_Should_Have_Error_For_Missing_Author()
        {
            // Arrange
            var document = new DocumentDTO
            {
                Id = 1,
                Name = "Valid Document",
                Author = string.Empty,
                Description = "This is a valid document.",
                LastModified = DateTime.Now
            };

            // Act & Assert
            var result = _validator.TestValidate(document);

            result.ShouldHaveValidationErrorFor(doc => doc.Author);
        }

        [Fact]
        public void Validate_Should_Have_Error_For_Author_Too_Long()
        {
            // Arrange
            var document = new DocumentDTO
            {
                Id = 1,
                Name = "Valid Document",
                Author = new string('A', 101),
                Description = "This is a valid document.",
                LastModified = DateTime.Now
            };

            // Act & Assert
            var result = _validator.TestValidate(document);

            result.ShouldHaveValidationErrorFor(doc => doc.Author);
        }

        [Fact]
        public void Validate_Should_Have_Error_For_Description_Too_Long()
        {
            // Arrange
            var document = new DocumentDTO
            {
                Id = 1,
                Name = "Valid Document",
                Author = "John Doe",
                Description = new string('A', 501),
                LastModified = DateTime.Now
            };

            // Act & Assert
            var result = _validator.TestValidate(document);

            result.ShouldHaveValidationErrorFor(doc => doc.Description);
        }
    }
}
