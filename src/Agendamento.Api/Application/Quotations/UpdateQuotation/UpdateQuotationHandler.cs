using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Quotations.UpdateQuotation;

public sealed class UpdateQuotationHandler(AgendamentoDbContext db, ITenantContext tenant)
{
    public async Task<bool> HandleAsync(Guid id, UpdateQuotationCommand command, CancellationToken ct = default)
    {
        if (!tenant.HasTenant) throw new InvalidOperationException("Unauthenticated tenant context.");
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (transaction != null)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Quotations\" WHERE \"Id\" = {id} FOR UPDATE", ct);
        var quotation = await db.Quotations.Include(q => q.Items).FirstOrDefaultAsync(q => q.Id == id, ct);
        if (quotation == null) return false;
        if (quotation.Status != QuotationStatus.Pending)
            throw new InvalidOperationException("Can only edit pending quotations.");
        if (!await db.Customers.AnyAsync(c => c.Id == command.CustomerId, ct))
            throw new ArgumentException("Customer not found.", nameof(command.CustomerId));
        if (command.Items == null || command.Items.Count == 0 || command.Items.Any(item => item == null))
            throw new ArgumentException("Quotation must contain items.", nameof(command.Items));
        var serviceIds = command.Items.Where(i => i.ServiceItemId.HasValue).Select(i => i.ServiceItemId!.Value).Distinct().ToList();
        if (await db.ServiceItems.CountAsync(s => serviceIds.Contains(s.Id), ct) != serviceIds.Count)
            throw new ArgumentException("Service item not found.", nameof(command.Items));

        var items = command.Items.Select(i => QuotationItem.Create(i.ServiceItemId, i.ServiceName, i.Unit,
            i.UnitPrice, i.Quantity, i.DiscountAmount)).ToList();
        var previousItems = quotation.Items.ToList();
        quotation.UpdatePendingContent(command.Code, command.CustomerId, command.IssueDate, command.ValidUntil,
            command.Notes, command.PaymentTerms, items, command.Deposit?.Type, command.Deposit?.Value ?? 0,
            command.Deposit?.PaymentNotes);
        db.RemoveRange(previousItems);
        db.AddRange(items);
        await db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return true;
    }
}
