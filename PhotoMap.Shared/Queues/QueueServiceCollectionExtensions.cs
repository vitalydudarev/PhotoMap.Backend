using Microsoft.Extensions.DependencyInjection;

namespace PhotoMap.Shared.Queues;

public static class QueueServiceCollectionExtensions
{
    public static IServiceCollection AddMessageQueue<T>(this IServiceCollection services, int capacity = 100)
    {
        return services.AddSingleton<IMessageQueue<T>>(_ => new ChannelMessageQueue<T>(capacity));
    }
}
