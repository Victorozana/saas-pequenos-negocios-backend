using Microsoft.EntityFrameworkCore;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.WorkOrders;
using Agendamento.Api.Domain.Financial;

namespace Agendamento.Api.Application.Dashboard.GetDashboardSummary;

public class GetDashboardSummaryHandler
{
    private readonly AgendamentoDbContext _dbContext;

    public GetDashboardSummaryHandler(AgendamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardSummaryDto> HandleAsync(GetDashboardSummaryQuery request, CancellationToken cancellationToken = default)
    {
        var receivablesQuery = _dbContext.ReceivableTitles.Include(r => r.Payments).AsNoTracking();
        
        if (request.StartDate.HasValue)
            receivablesQuery = receivablesQuery.Where(r => r.DueDate >= request.StartDate.Value);
            
        if (request.EndDate.HasValue)
            receivablesQuery = receivablesQuery.Where(r => r.DueDate <= request.EndDate.Value);

        var financialTitles = await receivablesQuery.ToListAsync(cancellationToken);

        var totalReceivedAmount = financialTitles.Sum(r => r.PaidAmount);
        var totalReceivableAmount = financialTitles.Where(r => r.Status == TransactionStatus.Pending || r.Status == TransactionStatus.Overdue || r.Status == TransactionStatus.PartiallyPaid).Sum(r => r.BalanceDue);

        var pendingWorkOrdersQuery = _dbContext.WorkOrders.AsNoTracking()
            .Where(w => w.Status == WorkOrderStatus.Scheduled || w.Status == WorkOrderStatus.InProgress);
            
        if (request.StartDate.HasValue)
            pendingWorkOrdersQuery = pendingWorkOrdersQuery.Where(w => w.CreatedAt >= request.StartDate.Value);
            
        if (request.EndDate.HasValue)
            pendingWorkOrdersQuery = pendingWorkOrdersQuery.Where(w => w.CreatedAt <= request.EndDate.Value);

        var pendingWorkOrdersCount = await pendingWorkOrdersQuery.CountAsync(cancellationToken);

        var quotationsQuery = _dbContext.Quotations.AsNoTracking();
        
        if (request.StartDate.HasValue)
            quotationsQuery = quotationsQuery.Where(q => q.IssueDate >= request.StartDate.Value);
            
        if (request.EndDate.HasValue)
            quotationsQuery = quotationsQuery.Where(q => q.IssueDate <= request.EndDate.Value);

        var quotations = await quotationsQuery
            .Select(q => new { q.Status })
            .ToListAsync(cancellationToken);

        var totalQuotations = quotations.Count;
        var approvedQuotations = quotations.Count(q => q.Status == QuotationStatus.Approved || q.Status == QuotationStatus.Converted);
        var conversionRatePercentage = totalQuotations == 0 ? 0 : (decimal)approvedQuotations / totalQuotations * 100;

        return new DashboardSummaryDto
        {
            TotalReceivedAmount = totalReceivedAmount,
            TotalReceivableAmount = totalReceivableAmount,
            PendingWorkOrdersCount = pendingWorkOrdersCount,
            ConversionMetrics = new ConversionMetricsDto
            {
                TotalQuotations = totalQuotations,
                ApprovedQuotations = approvedQuotations,
                ConversionRatePercentage = conversionRatePercentage
            }
        };
    }
}
