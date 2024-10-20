using System.ComponentModel.DataAnnotations;

namespace DMSystem.DAL.Models
{
    public class Document
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        public DateOnly LastModified { get; set; }

        [Required]
        public string Author { get; set; }

        public string? Description { get; set; }

        [Required]
        public byte[] Content { get; set; }  // Store PDF as binary data (byte array)
    }
}
