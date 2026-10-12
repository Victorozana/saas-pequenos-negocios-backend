using Agendamento.Api.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agendamento.Api.Infrastructure.Notifications;

public class NotificationOutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationOutboxProcessor> _logger;

    public NotificationOutboxProcessor(IServiceProvider serviceProvider, ILogger<NotificationOutboxProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Outbox Processor is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing outbox messages.");
            }

            // Polling interval of 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("Notification Outbox Processor is stopping.");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
    {
        // Use a new scope because DbContext is Scoped
        using var scope = _serviceProvider.CreateScope();
        
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notifications.ProcessPendingAsync(stoppingToken);
    }
}
