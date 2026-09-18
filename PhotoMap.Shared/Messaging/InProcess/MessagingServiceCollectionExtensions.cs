using Microsoft.Extensions.DependencyInjection;
using PhotoMap.Shared.Messaging.EventHandlerManager;
using PhotoMap.Shared.Messaging.Events;
using PhotoMap.Shared.Messaging.MessageSender;
using PhotoMap.Shared.Queues;

namespace PhotoMap.Shared.Messaging.InProcess;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-process replacement for the former RabbitMQ/NATS event transport.
    /// Event handlers are registered by the consuming application as <see cref="EventHandler.IEventHandler"/>.
    /// </summary>
    public static IServiceCollection AddInProcessMessaging(this IServiceCollection services)
    {
        services.AddMessageQueue<EventBase>();

        services.AddSingleton<InProcessMessageSender>();
        services.AddSingleton<IMessageSender>(provider => provider.GetRequiredService<InProcessMessageSender>());
        services.AddSingleton<IMessageSender2>(provider => provider.GetRequiredService<InProcessMessageSender>());

        services.AddSingleton<IEventHandlerManager, EventHandlerManager.EventHandlerManager>();
        services.AddHostedService<EventDispatcherBackgroundService>();

        return services;
    }
}
