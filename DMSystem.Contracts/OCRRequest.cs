using DMSystem.Contracts.DTOs;

namespace DMSystem.Contracts
{
    public class OCRRequest
    {
        public DocumentDTO Document { get; set; } = new DocumentDTO();
    }
}
