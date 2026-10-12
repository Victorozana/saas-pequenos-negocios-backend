namespace Agendamento.Api.Infrastructure.Persistence;

// Internal trusted ports only. Never resolve this choice from request input.
public sealed class AdministrativeDatabaseScope
{
    private int _depth;
    internal bool IsActive => _depth > 0;
    internal IDisposable Enter()
    {
        _depth++;
        return new Lease(this);
    }
    private sealed class Lease(AdministrativeDatabaseScope owner) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            owner._depth--;
            _disposed = true;
        }
    }
}
