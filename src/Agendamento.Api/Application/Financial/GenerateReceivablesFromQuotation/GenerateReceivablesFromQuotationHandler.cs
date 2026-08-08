using Agendamento.Api.Domain.Financial;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Persistence;

namespace Agendamento.Api.Application.Financial.GenerateReceivablesFromQuotation;

public class GenerateReceivablesFromQuotationHandler
{
    private readonly AgendamentoDbContext _dbContext;

    public GenerateReceivablesFromQuotationHandler(AgendamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(Quotation quotation, CancellationToken cancellationToken = default)
    {
        // 1. Check if deposit is required
        decimal depositAmount = 0;
        if (quotation.DepositInfo != null && quotation.DepositInfo.Value > 0)
        {
            if (quotation.DepositInfo.Type == DepositType.FixedAmount)
            {
                depositAmount = quotation.DepositInfo.Value;
            }
            else if (quotation.DepositInfo.Type == DepositType.Percentage)
            {
                depositAmount = quotation.TotalAmount * (quotation.DepositInfo.Value / 100m);
            }

            if (depositAmount > 0)
            {
                var depositTitle = ReceivableTitle.Create(
                    tenantId: quotation.TenantId,
                    description: $"Sinal/Entrada - Orçamento {quotation.Code}",
                    amount: depositAmount,
                    dueDate: DateTime.UtcNow, // Immediate due date for deposit
                    customerId: quotation.CustomerId,
                    quotationId: quotation.Id
                );

                _dbContext.ReceivableTitles.Add(depositTitle);
            }
        }

        // 2. Remaining balance
        var remainingAmount = quotation.TotalAmount - depositAmount;
        if (remainingAmount > 0)
        {
            var remainingTitle = ReceivableTitle.Create(
                tenantId: quotation.TenantId,
                description: $"Saldo - Orçamento {quotation.Code}",
                amount: remainingAmount,
                dueDate: DateTime.UtcNow.AddDays(30), // Default 30 days or based on terms
                customerId: quotation.CustomerId,
                quotationId: quotation.Id
            );

            _dbContext.ReceivableTitles.Add(remainingTitle);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
