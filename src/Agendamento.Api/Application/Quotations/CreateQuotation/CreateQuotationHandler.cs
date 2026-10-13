using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Quotations.CreateQuotation;

public class CreateQuotationHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public CreateQuotationHandler(AgendamentoDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> HandleAsync(CreateQuotationCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
            throw new InvalidOperationException("Unauthenticated tenant context.");

        if (command.Items == null || command.Items.Count == 0 || command.Items.Any(item => item == null))
            throw new ArgumentException("Quotation must contain valid items.", nameof(command.Items));

        // Check if customer exists in tenant
        var customerExists = await _dbContext.Customers
            .AnyAsync(c => c.Id == command.CustomerId, cancellationToken);
            
        if (!customerExists)
            throw new ArgumentException("Customer not found.", nameof(command.CustomerId));

        var serviceIds = command.Items.Where(i => i.ServiceItemId.HasValue)
            .Select(i => i.ServiceItemId!.Value).Distinct().ToList();
        if (await _dbContext.ServiceItems.CountAsync(s => serviceIds.Contains(s.Id), cancellationToken) != serviceIds.Count)
            throw new ArgumentException("Service item not found.", nameof(command.Items));

        var quotation = Quotation.Create(
            _tenantContext.TenantId,
            command.Code,
            command.CustomerId,
            command.IssueDate,
            command.ValidUntil,
            command.Notes,
            command.PaymentTerms);

        foreach (var itemDto in command.Items)
        {
            var item = QuotationItem.Create(
                itemDto.ServiceItemId,
                itemDto.ServiceName,
                itemDto.Unit,
                itemDto.UnitPrice,
                itemDto.Quantity,
                itemDto.DiscountAmount);
                
            quotation.AddItem(item);
        }

        if (command.Deposit != null)
        {
            quotation.SetDeposit(command.Deposit.Type, command.Deposit.Value, command.Deposit.PaymentNotes);
        }

        quotation.MarkAsPending();

        _dbContext.Quotations.Add(quotation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return quotation.Id;
    }
}
