using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Factories;

public interface IPhotoSourceDownloadServiceFactory
{
    IDownloadService GetService(PhotoSource photoSource);
}