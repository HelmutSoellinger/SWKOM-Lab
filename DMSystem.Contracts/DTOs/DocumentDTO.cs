using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FluentValidation;

namespace DMSystem.Contracts.DTOs
{
    public class DocumentDTO
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [MaxLength(100)]
        public string Author { get; set; }

        public DateTime LastModified { get; set; }

        public string? FilePath { get; set; }
    }

    public class DocumentDTOValidator : AbstractValidator<DocumentDTO>
    {
        public DocumentDTOValidator()
        {
            RuleFor(d => d.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

            RuleFor(d => d.Author)
                .NotEmpty().WithMessage("Author is required.")
                .MaximumLength(100).WithMessage("Author cannot exceed 100 characters.");
        }
    }
}
