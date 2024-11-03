using AutoMapper;
using DMSystem.DAL.Models;
using DMSystem.DTOs;

namespace DMSystem.Mappings
{
    public class DocumentProfile : Profile
    {
        public DocumentProfile()
        {
            // Map from Document entity to DocumentDTO and vice versa
            CreateMap<Document, DocumentDTO>();
            CreateMap<DocumentDTO, Document>()
                .ForMember(dest => dest.Content, opt => opt.Ignore()); // Ignore Content in the reverse mapping
        }
    }
}
