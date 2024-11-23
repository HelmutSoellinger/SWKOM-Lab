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
                .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => src.FilePath)) // Map FilePath
                .ForMember(dest => dest.LastModified, opt => opt.Ignore()); // LastModified is set in backend

            CreateMap<Document, DocumentDTO>()
                .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => src.FilePath)); // Map FilePath
        }
    }
}
