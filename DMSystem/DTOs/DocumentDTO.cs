using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace DMSystem.DTOs
{
    public class DocumentDTO
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        public string Author { get; set; }

        public string? Description { get; set; }

        // New FilePath property
        [Required]
        [MaxLength(500)] // Optional: Enforce a reasonable path length
        public string FilePath { get; set; }
    }

    public class DocumentDTOValidator : AbstractValidator<DocumentDTO>
    {
        public DocumentDTOValidator()
        {
            RuleFor(document => document.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

            RuleFor(document => document.Author)
                .NotEmpty().WithMessage("Author is required.");

            RuleFor(document => document.FilePath)
                .NotEmpty().WithMessage("File path is required.")
                .MaximumLength(500).WithMessage("File path cannot exceed 500 characters.");

            RuleFor(document => document.Description)
                .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.")
                .When(document => !string.IsNullOrEmpty(document.Description));
        }
    }
}
