using Agendamento.Domain.Identity;
using Agendamento.Infrastructure.Email;
using Agendamento.Infrastructure.Identity;
using Xunit;

namespace Agendamento.IntegrationTests.Identity;

public sealed class EmailVerificationOutboxTests
{
    [Fact(DisplayName = "Cadastro registra solicitação de verificação na caixa de saída sem token bruto @spec:AC-009 @principle:P-005")]
    public async Task Request_enqueues_a_verification_delivery_without_raw_token()
    {
        var outbox = new InMemoryEmailOutbox();
        var sender = new OutboxEmailVerificationSender(outbox);
        var requestedAt = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var passwords = new AspNetPasswordService();
        var user = User.Create("123.456.789-09", "admin@example.com", passwords.Hash("uma-senha-segura"));
        var token = EmailVerificationToken.Create(user.Id, "token-bruto-que-nao-vai-para-outbox", requestedAt);

        await sender.RequestAsync(user.Id, user.Email, token.Id, requestedAt);

        var message = Assert.Single(outbox.Messages);
        Assert.Equal(UserStatus.PendingEmailVerification, user.Status);
        Assert.Null(user.EmailVerifiedAtUtc);
        Assert.Equal(user.Id, message.UserId);
        Assert.Equal("admin@example.com", message.NormalizedEmail);
        Assert.Equal(token.Id, message.VerificationTokenId);
        Assert.Equal(requestedAt, message.OccurredAtUtc);
        Assert.Null(message.DispatchedAtUtc);
        Assert.Null(message.GetType().GetProperty("RawToken"));
    }

    private sealed class InMemoryEmailOutbox : IEmailOutbox
    {
        public List<Agendamento.Infrastructure.Persistence.Entities.EmailOutboxMessage> Messages { get; } = [];

        public Task EnqueueAsync(
            Agendamento.Infrastructure.Persistence.Entities.EmailOutboxMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
