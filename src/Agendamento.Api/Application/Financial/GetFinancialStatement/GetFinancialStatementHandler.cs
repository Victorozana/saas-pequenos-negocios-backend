using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Agendamento.Api.Domain.Financial;

namespace Agendamento.Api.Application.Financial.GetFinancialStatement;

public record FinancialStatementResult(
    decimal TotalReceivables,
    decimal TotalPayables,
    decimal TotalPaidReceivables,
    decimal TotalPaidPayables,
    decimal ProjectedBalance,
    decimal RealizedBalance
);

public class GetFinancialStatementHandler
{
    private readonly AgendamentoDbContext _dbContext;

    public GetFinancialStatementHandler(AgendamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FinancialStatementResult> HandleAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var receivables = await _dbContext.ReceivableTitles
            .Where(r => r.DueDate >= startDate && r.DueDate <= endDate && r.Status != TransactionStatus.Canceled)
            .ToListAsync(cancellationToken);

        var payables = await _dbContext.PayableTitles
            .Where(p => p.DueDate >= startDate && p.DueDate <= endDate && p.Status != TransactionStatus.Canceled)
            .ToListAsync(cancellationToken);

        var totalReceivables = receivables.Sum(r => r.OriginalAmount);
        var totalPayables = payables.Sum(p => p.OriginalAmount);
        
        var totalPaidReceivables = receivables.Sum(r => r.PaidAmount);
        var totalPaidPayables = payables.Sum(p => p.PaidAmount);

        var projectedBalance = totalReceivables - totalPayables;
        var realizedBalance = totalPaidReceivables - totalPaidPayables;

        return new FinancialStatementResult(
            TotalReceivables: totalReceivables,
            TotalPayables: totalPayables,
            TotalPaidReceivables: totalPaidReceivables,
            TotalPaidPayables: totalPaidPayables,
            ProjectedBalance: projectedBalance,
            RealizedBalance: realizedBalance
        );
    }
}
