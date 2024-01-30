using System.Text;
using Microsoft.Extensions.Options;
using NATS.Client;
using PhotoMap.Shared;
using PhotoMap.Shared.Messaging;
using PhotoMap.Shared.Messaging.EventHandler;
using PhotoMap.Shared.Messaging.EventHandlerManager;
using PhotoMap.Shared.Messaging.MessageListener;
using PhotoMap.Shared.Messaging.MessageSender;
using PhotoMap.Shared.Models;
using PhotoMap.Worker;
using PhotoMap.Worker.Handlers;
using PhotoMap.Worker.Services;
using PhotoMap.Worker.Services.Definitions;
using PhotoMap.Worker.Services.Implementations;
using PhotoMap.Worker.Services.Implementations.Core;
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

    string natsUrl = GetConfigurationProperty("NatsUrl");

    // register command handlers
    // services.AddSingleton<IEventHandler, StartProcessingEventHandler>();
    // services.AddSingleton<IEventHandler, PauseProcessingEventHandler>();
    // services.AddSingleton<IEventHandlerManager, EventHandlerManager>();

    // messaging
    // services.AddSingleton<IMessageListener, RabbitMqMessageListener>();
    // services.AddSingleton<IMessageSender2, RabbitMqMessageSender2>();
    services.AddScoped<IMessagingService>(_ => new NatsMessagingService(natsUrl));

    // Common services
    // services.AddSingleton<IDownloadManager, DownloadManager>();
    // services.AddSingleton<IProgressReporter, ProgressReporter>();
    // services.AddScoped<IImageProcessingServiceOld, ImageProcessingServiceOld>();
    services.AddScoped<IImageProcessingService, ImageProcessingService>();
    services.AddScoped<IExifExtractor, ExifExtractor>();

    // Yandex.Disk services
    // services.AddSingleton<IYandexDiskDownloadStateService, YandexDiskDownloadStateService>();
    // services.AddScoped<IYandexDiskDownloadService, YandexDiskDownloadService>();

    // services.AddHostedService<HostedService>();
    
    InitNATS(services.BuildServiceProvider());
}

string GetConfigurationProperty(string name)
{
    return builder.Configuration[name] ?? throw new Exception($"Configuration property {name} not specified.");
}

void InitNATS(IServiceProvider serviceProvider)
{
    string? natsUrl = builder.Configuration["NatsUrl"];
    if (string.IsNullOrEmpty(natsUrl))
    {
        throw new Exception("NATS Url is not specified in appsettings");
    }
                
    ConnectionFactory cf = new();
    IConnection natsConnection = cf.CreateConnection($"nats://{natsUrl}");

    ConfigureNats(natsConnection, serviceProvider);
}
        
// NATS listener
void ConfigureNats(IConnection natsConnection, IServiceProvider serviceProvider)
{
    natsConnection.SubscribeAsync("pm-ImageDownloaded", (sender, args) =>
    {
        if (args.Message.Data == null)
        {
            return;
        }

        string message = Encoding.UTF8.GetString(args.Message.Data);

        var processImageRequest = System.Text.Json.JsonSerializer.Deserialize<ProcessImageRequest>(message);
        if (processImageRequest != null)
        {
            using var serviceScope = serviceProvider.CreateScope();
            var imageProcessingService = serviceScope.ServiceProvider.GetService<IImageProcessingService>();
            if (imageProcessingService != null)
            {
                imageProcessingService.ProcessImage(processImageRequest.DownloadedFileInfo, processImageRequest.Sizes);
                // do action
            }
        }
    });
}