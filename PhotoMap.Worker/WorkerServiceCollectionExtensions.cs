using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhotoMap.Shared.Models;
using PhotoMap.Shared.Messages;
using PhotoMap.Shared.Messaging;
using PhotoMap.Worker.Services;
using PhotoMap.Worker.Services.Definitions;
using PhotoMap.Worker.Services.Implementations;
using PhotoMap.Worker.Services.Implementations.Core;

namespace PhotoMap.Worker;

public static class WorkerServiceCollectionExtensions
{
    /// <summary>
    /// Registers the worker: <see cref="ImageProcessingBackgroundService"/> receives the requests with the
    /// <see cref="IMessageReceiver{T}"/> of <see cref="ProcessImageRequest"/>, and sends the results with the
    /// <see cref="IMessageSender{T}"/> of <see cref="PhotoProcessed"/>, which the host registers: in memory, by the API
    /// running the worker without Kafka, or <see cref="AddWorkerKafkaTransports"/>.
    /// </summary>
    public static IServiceCollection AddWorker(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ImageProcessingSettings>(configuration.GetSection("ImageProcessing"));

        services.AddImageServices();
        services.AddHostedService<ImageProcessingBackgroundService>();

        return services;
    }

    /// <summary>
    /// The services working on images, without the worker: the API uses them as well, e.g. to convert the photos
    /// it serves, whether it runs the worker or not.
    /// </summary>
    public static IServiceCollection AddImageServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IImageProcessingService, ImageProcessingService>();
        services.TryAddSingleton<IExifExtractor, ExifExtractor>();
        services.TryAddSingleton<IImageConverter, ImageConverter>();

        return services;
    }

    /// <summary>
    /// The worker takes the requests from Kafka, as a member of <see cref="ImageProcessingBackgroundService.GroupId"/>,
    /// and sends the results to it.
    /// </summary>
    public static IServiceCollection AddWorkerKafkaTransports(this IServiceCollection services, KafkaSettings settings)
    {
        services.AddKafkaPublisher(settings);
        services.AddKafkaReceiver<ProcessImageRequest>(ProcessImageRequest.Topic, ImageProcessingBackgroundService.GroupId);
        services.AddKafkaSender<PhotoProcessed>(PhotoProcessed.Topic, a => a.GetKey());

        return services;
    }
}
