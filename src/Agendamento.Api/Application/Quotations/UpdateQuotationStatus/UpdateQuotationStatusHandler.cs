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
        var quotation = await _dbContext.Quotations
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (quotation == null)
            return false;

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

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (command.NewStatus == QuotationStatus.Approved)
        {
            await _generateReceivablesHandler.HandleAsync(quotation, cancellationToken);
        }

        return true;
    }
}
