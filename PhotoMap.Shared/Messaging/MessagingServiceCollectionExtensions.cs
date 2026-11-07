using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace PhotoMap.Shared.Messaging;

/// <summary>
/// Registers the transports of the messages, the same way in the API and in the worker host.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// The one producer every Kafka sender of the application shares.
    /// </summary>
    public static IServiceCollection AddKafkaPublisher(this IServiceCollection services, KafkaSettings settings)
    {
        services.TryAddSingleton(settings);
        services.TryAddSingleton<IMessagePublisher, KafkaMessagePublisher>();

        return services;
    }

    /// <summary>
    /// Sends the messages of type <typeparamref name="T"/> to <paramref name="topic"/>. Needs
    /// <see cref="AddKafkaPublisher"/>.
    /// </summary>
    public static IServiceCollection AddKafkaSender<T>(this IServiceCollection services, string topic, Func<T, string> getKey)
    {
        return services.AddSingleton<IMessageSender<T>>(provider =>
            new KafkaMessageSender<T>(provider.GetRequiredService<IMessagePublisher>(), topic, getKey));
    }

    /// <summary>
    /// Receives the messages of type <typeparamref name="T"/> from <paramref name="topic"/>, as a member of
    /// <paramref name="groupId"/>. Needs <see cref="AddKafkaPublisher"/>, for the settings.
    /// </summary>
    public static IServiceCollection AddKafkaReceiver<T>(this IServiceCollection services, string topic, string groupId)
    {
        return services.AddSingleton<IMessageReceiver<T>>(provider => new KafkaMessageReceiver<T>(
            provider.GetRequiredService<KafkaSettings>(), topic, groupId,
            provider.GetRequiredService<ILogger<KafkaMessageReceiver<T>>>()));
    }

    /// <summary>
    /// Sends the messages of type <typeparamref name="T"/> through an in-process queue, to a receiver in the same
    /// process.
    /// </summary>
    public static IServiceCollection AddChannelTransport<T>(this IServiceCollection services, int capacity = 100)
    {
        var transport = new ChannelMessageTransport<T>(capacity);

        services.AddSingleton<IMessageSender<T>>(transport);
        services.AddSingleton<IMessageReceiver<T>>(transport);

        return services;
    }
}
