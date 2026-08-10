using Agendamento.Api.Domain.Notifications;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
        
        var dbContext = scope.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<ICommunicationGateway>();

        // Disable query filters here since we want to process outbox for all tenants at once in the background
        var pendingMessages = await dbContext.NotificationMessages
            .IgnoreQueryFilters()
            .Where(m => m.Status == NotificationStatus.Pending && (m.NextRetryAt == null || m.NextRetryAt <= DateTime.UtcNow))
            .OrderBy(m => m.CreatedAt)
            .Take(50) // Process in batches
            .ToListAsync(stoppingToken);

        if (!pendingMessages.Any())
            return;

        _logger.LogInformation($"Found {pendingMessages.Count} pending notification(s) to process.");

        foreach (var message in pendingMessages)
        {
            try
            {
                var success = await gateway.SendAsync(message, stoppingToken);

                if (success)
                {
                    message.MarkAsSent();
                }
                else
                {
                    message.MarkAsFailed("Gateway returned false (simulated error).");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send message {message.Id}.");
                message.MarkAsFailed(ex.Message);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }
}
