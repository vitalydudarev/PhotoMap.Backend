using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhotoMap.Shared.Models;
using PhotoMap.Shared.Queues;
using PhotoMap.Worker.Services;
using PhotoMap.Worker.Services.Definitions;
using PhotoMap.Worker.Services.Implementations;
using PhotoMap.Worker.Services.Implementations.Core;

namespace PhotoMap.Worker;

public static class WorkerServiceCollectionExtensions
{
    /// <summary>
    /// Registers the worker inside the hosting application: images are taken from the in-process
    /// request queue, processed by <see cref="ImageProcessingBackgroundService"/> and put into the
    /// processed image queue.
    /// </summary>
    public static IServiceCollection AddWorker(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ImageProcessingSettings>(configuration.GetSection("ImageProcessing"));

        services.AddMessageQueue<ProcessImageRequest>();
        services.AddMessageQueue<ProcessedImage>();

        services.AddSingleton<IImageProcessingService, ImageProcessingService>();
        services.AddSingleton<IExifExtractor, ExifExtractor>();
        services.AddSingleton<IImageConverter, ImageConverter>();

        services.AddHostedService<ImageProcessingBackgroundService>();

        return services;
    }
}
