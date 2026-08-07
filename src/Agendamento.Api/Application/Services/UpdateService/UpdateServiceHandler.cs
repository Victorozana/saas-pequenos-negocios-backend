using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Services.UpdateService;

public class UpdateServiceHandler
{
    private readonly AgendamentoDbContext _dbContext;

    public UpdateServiceHandler(AgendamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HandleAsync(Guid id, UpdateServiceCommand command, CancellationToken cancellationToken = default)
    {
        var service = await _dbContext.ServiceItems
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (service == null)
            return false;

        service.Update(
            command.Name,
            command.Description,
            command.Unit,
            command.BasePrice);

        if (!command.IsActive && service.IsActive)
        {
            service.Inactivate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
