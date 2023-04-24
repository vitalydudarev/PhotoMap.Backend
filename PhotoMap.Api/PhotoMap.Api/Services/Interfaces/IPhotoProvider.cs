using System.Threading.Tasks;

namespace PhotoMap.Api.Services.Interfaces;

public interface IPhotoProvider
{
    Task<byte[]?> GetPhotoAsync(long id);
    Task<byte[]?> GetThumbAsync(long id, string size);
}