namespace Agendamento.Api.Infrastructure.Notifications;

public sealed class OverdueReceivableReminderWorker(IServiceScopeFactory scopes,
    ILogger<OverdueReceivableReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // No fixed operational hour: catch up at startup and reconcile throughout the day.
        // Durable daily keys make every subsequent poll/restart idempotent.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<OverdueReceivableReminderProcessor>().ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception)
            {
                logger.LogError("Falha ao preparar avisos diários de contas vencidas; o processamento será repetido.");
            }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
