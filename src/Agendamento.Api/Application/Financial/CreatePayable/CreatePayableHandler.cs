using Agendamento.Api.Domain.Financial;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Api.Application.Tenancy;

namespace Agendamento.Api.Application.Financial.CreatePayable;

public record CreatePayableCommand(string SupplierName, string Description, decimal Amount, DateTime DueDate);

public class CreatePayableHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreatePayableHandler(AgendamentoDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> HandleAsync(CreatePayableCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
            throw new InvalidOperationException("Tenant context is required.");

        var title = PayableTitle.Create(
            tenantId: _tenantContext.TenantId,
            supplierName: command.SupplierName,
            description: command.Description,
            amount: command.Amount,
            dueDate: command.DueDate
        );

        _dbContext.PayableTitles.Add(title);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return title.Id;
    }
}
