using AutoMapper;

namespace PhotoMap.Api.Database.Mappers;

public class ModelEntityMappingProfile : Profile
{
    public ModelEntityMappingProfile()
    {
        CreateMap<Domain.Models.Photo, Database.Entities.Photo>().ReverseMap();
    }
}
