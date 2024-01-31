using System.Text;
using NATS.Client;
using PhotoMap.Shared.Models;

namespace PhotoMap.Worker.Services;

public class NatsBackgroundService : BackgroundService
{
    private readonly ILogger<NatsBackgroundService> _logger;
    private readonly IConnection _natsConnection;
    private readonly IRequestQueueService _requestQueueService;

    public NatsBackgroundService(
        ILogger<NatsBackgroundService> logger,
        IConnection natsConnection,
        IRequestQueueService requestQueueService)
    {
        _logger = logger;
        _natsConnection = natsConnection;
        _requestQueueService = requestQueueService;
    }
    
    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(NatsBackgroundService)} is stopping.");

        await base.StopAsync(stoppingToken);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(NatsBackgroundService)} is running.");
        
        _natsConnection.SubscribeAsync("pm-ImageDownloaded", (sender, args) =>
        {
            if (args.Message.Data == null)
            {
                return;
            }

            string message = Encoding.UTF8.GetString(args.Message.Data);

            var processImageRequest = System.Text.Json.JsonSerializer.Deserialize<ProcessImageRequest>(message);
            if (processImageRequest != null)
            {
                _requestQueueService.Enqueue(processImageRequest);
            }
        });
        
        await Task.CompletedTask;
    }
}