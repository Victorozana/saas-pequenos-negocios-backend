using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Quotations.GetQuotations;

public class GetQuotationsHandler
{
    private readonly AgendamentoDbContext _dbContext;

    public GetQuotationsHandler(AgendamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<QuotationListDto>> HandleAsync(GetQuotationsQuery query, CancellationToken cancellationToken = default)
    {
        var queryable = _dbContext.Quotations
            .Include(q => q.Customer)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = query.SearchTerm.ToLower();
            queryable = queryable.Where(q => 
                q.Code.ToLower().Contains(search) || 
                (q.Customer != null && q.Customer.Name.ToLower().Contains(search)));
        }

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(q => q.Status == query.Status.Value);
        }

        var totalCount = await queryable.CountAsync(cancellationToken);

        var items = await queryable
            .OrderByDescending(q => q.IssueDate)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(q => new QuotationListDto(
                q.Id,
                q.Code,
                q.Customer != null ? q.Customer.Name : "N/A",
                q.IssueDate,
                q.ValidUntil,
                q.Status,
                q.TotalAmount))
            .ToListAsync(cancellationToken);

        return new PagedResult<QuotationListDto>(items, totalCount, query.Page, query.PageSize);
    }
}
