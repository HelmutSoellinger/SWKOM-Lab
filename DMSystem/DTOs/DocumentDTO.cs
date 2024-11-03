using System.ComponentModel.DataAnnotations;

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
        public string? Description { get; set; }
    }
}
