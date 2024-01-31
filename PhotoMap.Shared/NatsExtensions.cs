using Microsoft.Extensions.DependencyInjection;
using NATS.Client;

namespace PhotoMap.Shared;

public static class NatsExtensions
{
    public static IServiceCollection AddNats(this IServiceCollection services, Action<Options> configOptions)
    {
        services.AddSingleton<ConnectionFactory>();

        var options = ConnectionFactory.GetDefaultOptions();
        configOptions(options);

        services.AddSingleton<Options>(options);

        services.AddSingleton<IConnection>(provider =>
        {
            var connectionFactory = provider.GetRequiredService<ConnectionFactory>();
            var connectionOptions = provider.GetRequiredService<Options>();
            
            return connectionFactory.CreateConnection(connectionOptions);
        });

        return services;
    }
}