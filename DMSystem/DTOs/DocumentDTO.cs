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
        [Required]
        public DateOnly LastModified { get; set; }
        public string? Description { get; set; } = string.Empty;
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

            RuleFor(document => document.LastModified)
                .NotEmpty().WithMessage("LastModified date is required.");

            RuleFor(document => document.Description)
                .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.")
                .When(document => document.Description != null); // Only validate if Description is not null
        }
    }
}
