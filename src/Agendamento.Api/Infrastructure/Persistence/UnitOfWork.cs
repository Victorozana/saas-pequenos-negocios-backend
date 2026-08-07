using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Common;
using Microsoft.EntityFrameworkCore.Storage;

namespace Agendamento.Api.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AgendamentoDbContext _db;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(AgendamentoDbContext db)
    {
        _db = db;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            // In-Memory DB doesn't support transactions. Just mock it for testing.
            return;
        }
        _transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
