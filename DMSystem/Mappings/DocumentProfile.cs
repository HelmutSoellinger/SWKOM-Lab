using AutoMapper;
using DMSystem.DAL.Models;
using DMSystem.Contracts.DTOs;

namespace DMSystem.Mappings
{
    public class DocumentProfile : Profile
    {
        public DocumentProfile()
        {
            // Ignore LastModified during mapping
            CreateMap<DocumentDTO, Document>()
                .ForMember(dest => dest.LastModified, opt => opt.Ignore());

            CreateMap<Document, DocumentDTO>()
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => src.LastModified));

            // Ignore FilePath during mapping
            CreateMap<Document, DocumentDTO>()
            .ForMember(dest => dest.FilePath, opt => opt.Ignore());

            CreateMap<Document, DocumentDTO>()
                .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => src.FilePath));
        }
    }
}
