using Agendamento.Api.Application.Common;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.WorkOrders;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.WorkOrders.ConvertQuotationToWorkOrder;

public class ConvertQuotationToWorkOrderHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public ConvertQuotationToWorkOrderHandler(
        AgendamentoDbContext dbContext,
        ITenantContext tenantContext,
        IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> HandleAsync(ConvertQuotationToWorkOrderCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
            throw new InvalidOperationException("Unauthenticated tenant context.");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Use the same row lock as approval and editing. Read the order only
            // after acquiring it so a concurrent conversion is observed on replay.
            if (_dbContext.Database.IsRelational())
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Quotations\" WHERE \"Id\" = {command.QuotationId} AND tenant_id = {_tenantContext.TenantId} FOR UPDATE", cancellationToken);

            var existingOrder = await _dbContext.WorkOrders
                .FirstOrDefaultAsync(w => w.QuotationId == command.QuotationId && w.TenantId == _tenantContext.TenantId, cancellationToken);
            if (existingOrder != null)
            {
                await _unitOfWork.CommitAsync(cancellationToken);
                return existingOrder.Id;
            }

            var quotation = await _dbContext.Quotations
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == command.QuotationId && q.TenantId == _tenantContext.TenantId, cancellationToken);

            if (quotation == null)
                throw new InvalidOperationException("Quotation not found.");

            // Validations for conversion (e.g. status must be Approved)
            if (quotation.Status != QuotationStatus.Approved)
                throw new InvalidOperationException("Quotation must be approved before converting to a work order.");

            // Domain method validates and creates the work order
            var workOrder = WorkOrder.CreateFromQuotation(quotation);
            quotation.MarkAsConverted();

            _dbContext.WorkOrders.Add(workOrder);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return workOrder.Id;
        }
        catch
        {
            // A failed relational save rolls back all rows. The caller must
            // discard this scope; rollback does not restore EF tracked values.
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
