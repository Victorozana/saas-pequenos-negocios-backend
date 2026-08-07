using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Services.GetServices;

public class GetServicesHandler
{
    private readonly AgendamentoDbContext _dbContext;

    public GetServicesHandler(AgendamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<ServiceItemDto>> HandleAsync(GetServicesQuery query, CancellationToken cancellationToken = default)
    {
        var queryable = _dbContext.ServiceItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = query.SearchTerm.ToLower();
            queryable = queryable.Where(s => s.Name.ToLower().Contains(search) || 
                                            (s.Description != null && s.Description.ToLower().Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            queryable = queryable.Where(s => s.IsActive == query.IsActive.Value);
        }

        var totalCount = await queryable.CountAsync(cancellationToken);

        var items = await queryable
            .OrderBy(s => s.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new ServiceItemDto(
                s.Id,
                s.Name,
                s.Description,
                s.Unit,
                s.BasePrice,
                s.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<ServiceItemDto>(items, totalCount, query.Page, query.PageSize);
    }
}
