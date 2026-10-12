using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Quotations.UpdateQuotationStatus;

public record UpdateQuotationStatusCommand(QuotationStatus NewStatus);

public class UpdateQuotationStatusHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly Agendamento.Api.Application.Financial.GenerateReceivablesFromQuotation.GenerateReceivablesFromQuotationHandler _generateReceivablesHandler;

    public UpdateQuotationStatusHandler(
        AgendamentoDbContext dbContext,
        Agendamento.Api.Application.Financial.GenerateReceivablesFromQuotation.GenerateReceivablesFromQuotationHandler generateReceivablesHandler)
    {
        _dbContext = dbContext;
        _generateReceivablesHandler = generateReceivablesHandler;
    }

    public async Task<bool> HandleAsync(Guid id, UpdateQuotationStatusCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        if (transaction != null)
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Quotations\" WHERE \"Id\" = {id} FOR UPDATE", cancellationToken);
        var quotation = await _dbContext.Quotations
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (quotation == null)
            return false;

        if (command.NewStatus == QuotationStatus.Approved &&
            (quotation.Status == QuotationStatus.Approved || quotation.Status == QuotationStatus.Converted))
            return true;

        quotation.CheckExpiration();

        switch (command.NewStatus)
        {
            case QuotationStatus.Pending:
                quotation.MarkAsPending();
                break;
            case QuotationStatus.Approved:
                quotation.Approve();
                break;
            case QuotationStatus.Rejected:
                quotation.Reject();
                break;
            case QuotationStatus.Canceled:
                quotation.Cancel();
                break;
            default:
                throw new InvalidOperationException($"Cannot transition to status {command.NewStatus} directly.");
        }

        if (command.NewStatus == QuotationStatus.Approved)
        {
            await _generateReceivablesHandler.HandleAsync(quotation, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);

        return true;
    }
}
