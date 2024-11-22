using AutoMapper;
using DMSystem.DAL.Models;
using DMSystem.DTOs;

namespace DMSystem.Mappings
{
    public class DocumentProfile : Profile
    {
        public DocumentProfile()
        {
            CreateMap<DocumentDTO, Document>()
                .ForMember(dest => dest.Content, opt => opt.Ignore()) // Content is handled manually
                .ForMember(dest => dest.LastModified, opt => opt.Ignore()); // LastModified is set in backend

            CreateMap<Document, DocumentDTO>();
        }
    }
}
