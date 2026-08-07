using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Services.GetServices;

public class GetServiceByIdHandler
{
    private readonly AgendamentoDbContext _dbContext;

    public GetServiceByIdHandler(AgendamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceItemDto?> HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _dbContext.ServiceItems
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (service == null)
            return null;

        return new ServiceItemDto(
            service.Id,
            service.Name,
            service.Description,
            service.Unit,
            service.BasePrice,
            service.IsActive);
    }
}
