namespace PhotoMap.Api.Domain.Services;

public interface IFrontendNotificationService
{
    Task SendErrorAsync(long userId, long sourceId, string errorText);
    Task SendProgressAsync(long userId, long sourceId, int processed, int total);
}