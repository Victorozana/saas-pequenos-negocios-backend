using Agendamento.Infrastructure.Persistence.Entities;

namespace Agendamento.Infrastructure.Email;

public interface IEmailOutbox
{
    Task EnqueueAsync(EmailOutboxMessage message, CancellationToken cancellationToken);
}
