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
                .ForMember(dest => dest.LastModified, opt => opt.Ignore());

            CreateMap<Document, DocumentDTO>()
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => src.LastModified));
        }
    }
}
