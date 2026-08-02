using Agendamento.Application.Identity;
using Agendamento.Infrastructure.Persistence.Entities;

namespace Agendamento.Infrastructure.Email;

public sealed class OutboxEmailVerificationSender(IEmailOutbox outbox) : IEmailVerificationSender
{
    public Task RequestAsync(
        Guid userId,
        string normalizedEmail,
        Guid verificationTokenId,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default) =>
        outbox.EnqueueAsync(
            EmailOutboxMessage.Create(userId, normalizedEmail, verificationTokenId, requestedAtUtc),
            cancellationToken);
}
