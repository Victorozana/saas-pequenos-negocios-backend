using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Quotations.UpdateQuotationStatus;

public record UpdateQuotationStatusCommand(QuotationStatus NewStatus);

public class UpdateQuotationStatusHandler
{
    private readonly AgendamentoDbContext _dbContext;

    public UpdateQuotationStatusHandler(AgendamentoDbContext dbContext)
    {
        _dbContext = dbContext;
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
        return true;
    }
}
