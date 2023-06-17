using Microsoft.Extensions.Options;
using PhotoMap.Shared;
using PhotoMap.Shared.Messaging;
using PhotoMap.Shared.Messaging.EventHandler;
using PhotoMap.Shared.Messaging.EventHandlerManager;
using PhotoMap.Shared.Messaging.MessageListener;
using PhotoMap.Shared.Messaging.MessageSender;
using PhotoMap.Worker;
using PhotoMap.Worker.Handlers;
using PhotoMap.Worker.Services;
using PhotoMap.Worker.Services.Definitions;
using PhotoMap.Worker.Services.Implementations;
using PhotoMap.Worker.Settings;

var builder = WebApplication.CreateBuilder(args);

ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

// ConfigureMiddleware(app, app.Services);
// ConfigureEndpoints(app, app.Services);

app.Run();

void ConfigureServices(IServiceCollection services, ConfigurationManager configuration)
{
    services.AddHttpClient();

    services.Configure<StorageServiceSettings>(configuration.GetSection("Storage"));
    services.Configure<ImageProcessingSettings>(configuration.GetSection("ImageProcessing"));
    services.Configure<Dictionary<string, RabbitMqSettings>>(configuration.GetSection("RabbitMQ"));

    services.AddSingleton(a =>
    {
        var settingsDictionary = a.GetRequiredService<IOptions<Dictionary<string, RabbitMqSettings>>>().Value;

        return settingsDictionary.ToDictionary(a => a.Key, b => new RabbitMqConfiguration
        {
            HostName = b.Value.HostName,
            Port = b.Value.Port,
            UserName = b.Value.UserName,
            Password = b.Value.Password,
            ConsumeQueueName = b.Value.InQueueName,
            ResponseQueueName = b.Value.OutQueueName
        });
    });

    services.AddSingleton(a =>
    {
        var settingsDictionary = a.GetRequiredService<IOptions<Dictionary<string, RabbitMqSettings>>>().Value;
        var settings = settingsDictionary.First(key => key.Key == ApiConstants.ImageServiceApi).Value;

        return new RabbitMqConfiguration
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            ConsumeQueueName = settings.InQueueName,
            ResponseQueueName = settings.OutQueueName
        };
    });

    // register command handlers
    services.AddSingleton<IEventHandler, StartProcessingEventHandler>();
    services.AddSingleton<IEventHandler, PauseProcessingEventHandler>();
    services.AddSingleton<IEventHandlerManager, EventHandlerManager>();

    services.AddSingleton<IMessageListener, RabbitMqMessageListener>();
    services.AddSingleton<IMessageSender2, RabbitMqMessageSender2>();

    // Common services
    services.AddSingleton<IDownloadManager, DownloadManager>();
    services.AddSingleton<IProgressReporter, ProgressReporter>();
    services.AddScoped<IImageProcessingService, ImageProcessingService>();

    // Yandex.Disk services
    services.AddSingleton<IYandexDiskDownloadStateService, YandexDiskDownloadStateService>();
    services.AddScoped<IYandexDiskDownloadService, YandexDiskDownloadService>();

    services.AddHostedService<HostedService>();
}