using DMSystem.DTOs;
using FluentValidation.TestHelper;
using Xunit;

namespace DMSystem.Tests
{
    public class DocumentDTOValidatorTests
    {
        private readonly DocumentDTOValidator _validator;

        public DocumentDTOValidatorTests()
        {
            _validator = new DocumentDTOValidator();
        }

        [Fact]
        public void Validate_Document_Should_Have_Error_For_Null_Name()
        {
            // Arrange
            var document = new DocumentDTO { Name = null };

            // Act & Assert
            var result = _validator.TestValidate(document);
            result.ShouldHaveValidationErrorFor(doc => doc.Name);
        }

        [Fact]
        public void Validate_Document_Should_Have_Error_For_Empty_Name()
        {
            // Arrange
            var document = new DocumentDTO { Name = string.Empty };

            // Act & Assert
            var result = _validator.TestValidate(document);
            result.ShouldHaveValidationErrorFor(doc => doc.Name);
        }

        [Fact]
        public void Validate_Document_Should_Have_Error_For_Name_Larger_Than_Max_Length()
        {
            // Arrange
            var document = new DocumentDTO { Name = new string('a', 101) };

            // Act & Assert
            var result = _validator.TestValidate(document);
            result.ShouldHaveValidationErrorFor(doc => doc.Name);
        }

        [Fact]
        public void Validate_Document_Should_Have_No_Error_For_Valid_Name()
        {
            // Arrange
            var document = new DocumentDTO { Name = "Valid Document" };

            // Act & Assert
            var result = _validator.TestValidate(document);
            result.ShouldNotHaveValidationErrorFor(doc => doc.Name);
        }

        [Fact]
        public void Validate_Document_Should_Have_Error_For_Null_Author()
        {
            // Arrange
            var document = new DocumentDTO { Author = null };

            // Act & Assert
            var result = _validator.TestValidate(document);
            result.ShouldHaveValidationErrorFor(doc => doc.Author);
        }

        [Fact]
        public void Validate_Document_Should_Have_No_Error_For_Valid_Author()
        {
            // Arrange
            var document = new DocumentDTO { Author = "John Doe" };

            // Act & Assert
            var result = _validator.TestValidate(document);
            result.ShouldNotHaveValidationErrorFor(doc => doc.Author);
        }

        [Fact]
        public void Validate_Document_Should_Allow_Empty_Description()
        {
            // Arrange
            var document = new DocumentDTO();

            // Act & Assert
            var result = _validator.TestValidate(document);
            result.ShouldNotHaveValidationErrorFor(doc => doc.Description);
        }

        [Fact]
        public void Validate_Document_Should_Have_Error_For_Description_Larger_Than_Max_Length()
        {
            // Arrange
            var document = new DocumentDTO { Description = new string('a', 501) };

            // Act & Assert
            var result = _validator.TestValidate(document);
            result.ShouldHaveValidationErrorFor(doc => doc.Description);
        }
    }
}
