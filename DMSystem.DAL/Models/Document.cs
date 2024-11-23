using System.ComponentModel.DataAnnotations;

namespace DMSystem.DAL.Models
{
    public class Document
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public DateTime LastModified { get; set; }

        [Required]
        public string Author { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [MaxLength(500)] // Adjust max length as needed
        public string FilePath { get; set; } = string.Empty;
    }
}
