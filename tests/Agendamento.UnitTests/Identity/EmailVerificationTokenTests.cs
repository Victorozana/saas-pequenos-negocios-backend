using Agendamento.Domain.Identity;
using Agendamento.Application.Identity.VerifyEmail;
using Xunit;

namespace Agendamento.UnitTests.Identity;

public sealed class EmailVerificationTokenTests
{
    [Fact(DisplayName = "Token de e-mail é armazenado somente como digest e expira em 24 horas @spec:AC-009 @principle:P-005")]
    public void Create_stores_digest_and_sets_24_hour_expiration()
    {
        var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        const string rawToken = "raw-token-that-must-not-be-persisted";

        var token = EmailVerificationToken.Create(Guid.NewGuid(), rawToken, now);

        Assert.NotEqual(rawToken, token.Digest);
        Assert.Equal(64, token.Digest.Length);
        Assert.Equal(now.AddHours(24), token.ExpiresAtUtc);
        Assert.Null(token.ConsumedAtUtc);
    }

    [Fact(DisplayName = "Token válido é consumido somente uma vez @spec:AC-010")]
    public void TryConsume_allows_single_use_before_expiration()
    {
        var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var token = EmailVerificationToken.Create(Guid.NewGuid(), "valid-token", now);

        Assert.True(token.TryConsume(now.AddMinutes(1)));
        Assert.False(token.TryConsume(now.AddMinutes(2)));
        Assert.Equal(now.AddMinutes(1), token.ConsumedAtUtc);
    }

    [Fact(DisplayName = "Token vencido não pode ser consumido @spec:AC-011")]
    public void TryConsume_rejects_expired_token()
    {
        var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var token = EmailVerificationToken.Create(Guid.NewGuid(), "expired-token", now);

        Assert.False(token.TryConsume(now.AddHours(24).AddTicks(1)));
        Assert.Null(token.ConsumedAtUtc);
    }

    [Fact(DisplayName = "Confirmação válida verifica o e-mail uma única vez @spec:AC-010")]
    public async Task Handler_confirms_email_once_for_a_valid_token()
    {
        var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var user = new FakeUser(Guid.NewGuid());
        var token = EmailVerificationToken.Create(user.Id, "valid-token", now);
        var tokens = new FakeTokenRepository(token);
        var handler = new VerifyEmailHandler(tokens, new FakeUserRepository(user), new FakeClock(now.AddMinutes(1)));

        var first = await handler.HandleAsync(new VerifyEmailCommand("valid-token"));
        var repeated = await handler.HandleAsync(new VerifyEmailCommand("valid-token"));

        Assert.Equal(VerifyEmailResult.Verified, first);
        Assert.Equal(VerifyEmailResult.InvalidToken, repeated);
        Assert.Equal(1, user.ConfirmationCount);
        Assert.NotNull(token.ConsumedAtUtc);
    }

    [Theory(DisplayName = "Token inválido, adulterado ou vencido não verifica o e-mail @spec:AC-011")]
    [InlineData("missing-token")]
    [InlineData("tampered-token")]
    public async Task Handler_rejects_invalid_tokens_without_confirming_email(string submittedToken)
    {
        var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var user = new FakeUser(Guid.NewGuid());
        var token = EmailVerificationToken.Create(user.Id, "valid-token", now);
        var handler = new VerifyEmailHandler(
            new FakeTokenRepository(token),
            new FakeUserRepository(user),
            new FakeClock(now.AddHours(25)));

        var result = await handler.HandleAsync(new VerifyEmailCommand(submittedToken));

        Assert.Equal(VerifyEmailResult.InvalidToken, result);
        Assert.Equal(0, user.ConfirmationCount);
        Assert.Null(token.ConsumedAtUtc);
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class FakeTokenRepository(EmailVerificationToken token) : IEmailVerificationTokenRepository
    {
        public Task<EmailVerificationToken?> FindByDigestAsync(string digest, CancellationToken cancellationToken) =>
            Task.FromResult<EmailVerificationToken?>(token.Digest == digest ? token : null);

        public Task SaveAsync(EmailVerificationToken token, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeUserRepository(FakeUser user) : IEmailVerificationUserRepository
    {
        public Task<IEmailVerificationUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IEmailVerificationUser?>(user.Id == id ? user : null);

        public Task SaveAsync(IEmailVerificationUser user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeUser(Guid id) : IEmailVerificationUser
    {
        public Guid Id { get; } = id;

        public int ConfirmationCount { get; private set; }

        public void ConfirmEmail(DateTimeOffset confirmedAtUtc) => ConfirmationCount++;
    }
}
