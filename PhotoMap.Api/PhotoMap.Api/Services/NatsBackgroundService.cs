using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;

namespace PhotoMap.Api.Services;

public class NatsBackgroundService : BackgroundService
{
    private readonly ILogger<NatsBackgroundService> _logger;
    private readonly IConnection _natsConnection;

    public NatsBackgroundService(ILogger<NatsBackgroundService> logger, IConnection natsConnection)
    {
        _logger = logger;
        _natsConnection = natsConnection;
    }
    
    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(NatsBackgroundService)} is stopping.");

        await base.StopAsync(stoppingToken);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(NatsBackgroundService)} is running.");
        
        /*_natsConnection.SubscribeAsync("pm-ImageDownloaded", (sender, args) =>
        {
            if (args.Message.Data == null)
            {
                return;
            }

            string message = Encoding.UTF8.GetString(args.Message.Data);
        });*/
        
        _natsConnection.SubscribeAsync("pm-ImageProcessed", (sender, args) =>
        {
            if (args.Message.Data == null)
            {
                return;
            }

            string message = Encoding.UTF8.GetString(args.Message.Data);
        });
        
        await Task.CompletedTask;
    }
}