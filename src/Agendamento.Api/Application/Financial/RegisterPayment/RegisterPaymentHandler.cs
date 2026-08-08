using Agendamento.Api.Domain.Financial;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Financial.RegisterPayment;

public record RegisterPaymentCommand(decimal Amount, PaymentMethod Method, DateTime PaymentDate, string? Notes);

public class RegisterReceivablePaymentHandler
{
    private readonly AgendamentoDbContext _dbContext;

    public RegisterReceivablePaymentHandler(AgendamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HandleAsync(Guid receivableId, RegisterPaymentCommand command, CancellationToken cancellationToken = default)
    {
        var title = await _dbContext.ReceivableTitles
            .Include(t => t.Payments)
            .FirstOrDefaultAsync(t => t.Id == receivableId, cancellationToken);

        if (title == null)
            return false;

        title.RegisterPayment(command.Amount, command.Method, command.PaymentDate, command.Notes);
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class RegisterPayablePaymentHandler
{
    private readonly AgendamentoDbContext _dbContext;

    public RegisterPayablePaymentHandler(AgendamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HandleAsync(Guid payableId, RegisterPaymentCommand command, CancellationToken cancellationToken = default)
    {
        var title = await _dbContext.PayableTitles
            .Include(t => t.Payments)
            .FirstOrDefaultAsync(t => t.Id == payableId, cancellationToken);

        if (title == null)
            return false;

        title.RegisterPayment(command.Amount, command.Method, command.PaymentDate, command.Notes);
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
