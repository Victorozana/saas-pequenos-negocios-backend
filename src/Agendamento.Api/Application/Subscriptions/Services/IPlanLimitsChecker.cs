namespace Agendamento.Api.Application.Subscriptions.Services;

public interface IPlanLimitsChecker
{
    Task<bool> CanAddUserAsync(CancellationToken cancellationToken = default);
    Task<bool> CanAddCustomerAsync(CancellationToken cancellationToken = default);
    Task<bool> CanAddQuotationAsync(CancellationToken cancellationToken = default);
    Task<bool> IsSubscriptionWriteAllowedAsync(CancellationToken cancellationToken = default);
}
