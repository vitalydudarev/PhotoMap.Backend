using NATS.Client;
using PhotoMap.Shared;
using PhotoMap.Shared.Messaging.MessageSender;
using PhotoMap.Worker.Services;
using PhotoMap.Worker.Services.Definitions;
using PhotoMap.Worker.Services.Implementations;
using PhotoMap.Worker.Services.Implementations.Core;

var builder = WebApplication.CreateBuilder(args);

ConfigureServices(builder.Services);

var app = builder.Build();

app.Run();

void ConfigureServices(IServiceCollection services)
{
    services.AddHttpClient();

    string natsUrl = GetConfigurationProperty("NatsUrl");

    services.AddSingleton<IMessagingService, NatsMessagingService>();

    services.AddSingleton<IImageProcessingService, ImageProcessingService>();
    services.AddSingleton<IExifExtractor, ExifExtractor>();

    services.AddHostedService<NatsBackgroundService>();
    services.AddHostedService<RequestQueueBackgroundService>();

    services.AddSingleton<IRequestQueueService, RequestQueueService>();

    services.AddNats(options =>
    {
        options.Url = natsUrl;
        options.AsyncErrorEventHandler += AsyncErrorEventHandler;

        void AsyncErrorEventHandler(object? sender, ErrEventArgs e)
        {
            int a = 4;
        }
    });
}

string GetConfigurationProperty(string name)
{
    return builder.Configuration[name] ?? throw new Exception($"Configuration property {name} not specified.");
}
