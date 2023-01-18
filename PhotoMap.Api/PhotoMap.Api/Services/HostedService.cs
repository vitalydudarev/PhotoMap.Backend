using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoMap.Shared.Messaging.MessageListener;

namespace PhotoMap.Api.Services
{
    public class HostedService : BackgroundService
    {
        private readonly IMessageListener _messageListener;
        private readonly ILogger<HostedService> _logger;

        public HostedService(IMessageListener messageListener, ILogger<HostedService> logger)
        {
            _messageListener = messageListener;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Hosted Service running");

            return base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            _logger.LogInformation("ExecuteAsync");

            _messageListener.Listen(stoppingToken);

            await Task.CompletedTask;
        }
    }
}
