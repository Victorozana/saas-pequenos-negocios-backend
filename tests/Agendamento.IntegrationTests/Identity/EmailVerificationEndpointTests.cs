using Agendamento.Application.Identity.VerifyEmail;
using System.Net.Http.Json;
using Agendamento.IntegrationTests.Infrastructure;
using Agendamento.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Agendamento.IntegrationTests.Identity;

public sealed class EmailVerificationEndpointTests
{
    [Fact(DisplayName = "@spec:AC-010 endpoint confirms a valid email-verification token once")]
    public async Task Valid_token_confirms_the_email_once()
    {
        var user = new FakeVerificationUser(Guid.NewGuid());
        var token = EmailVerificationToken.Create(user.Id, "token-valid", DateTimeOffset.UtcNow);
        using var client = CreateClient(token, user);

        var first = await client.PostAsJsonAsync("/api/v1/auth/email-verifications", new { token = "token-valid" });
        var second = await client.PostAsJsonAsync("/api/v1/auth/email-verifications", new { token = "token-valid" });

        Assert.Equal(System.Net.HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(1, user.Confirmations);
    }

    [Fact(DisplayName = "@spec:AC-011 endpoint rejects invalid email-verification token without personal data")]
    public async Task Invalid_token_does_not_confirm_the_email()
    {
        var user = new FakeVerificationUser(Guid.NewGuid());
        using var client = CreateClient(null, user);

        var result = await client.PostAsJsonAsync("/api/v1/auth/email-verifications", new { token = "adulterado" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("application/problem+json", result.Content.Headers.ContentType!.MediaType);
        Assert.Equal(0, user.Confirmations);
    }

    private static HttpClient CreateClient(EmailVerificationToken? token, FakeVerificationUser user) =>
        new AgendamentoApiFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IEmailVerificationTokenRepository>(new InMemoryTokens(token));
            services.AddSingleton<IEmailVerificationUserRepository>(new InMemoryUsers(user));
            services.AddSingleton<IClock>(new FixedClock(DateTimeOffset.UtcNow));
            services.AddTransient<VerifyEmailHandler>();
        })).CreateClient();

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class InMemoryTokens(EmailVerificationToken? token) : IEmailVerificationTokenRepository
    {
        private readonly EmailVerificationToken? _token = token;

        public Task<EmailVerificationToken?> FindByDigestAsync(string digest, CancellationToken cancellationToken) =>
            Task.FromResult(_token?.Digest == digest ? _token : null);

        public Task SaveAsync(EmailVerificationToken token, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryUsers(FakeVerificationUser user) : IEmailVerificationUserRepository
    {
        public Task<IEmailVerificationUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IEmailVerificationUser?>(user.Id == id ? user : null);

        public Task SaveAsync(IEmailVerificationUser user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeVerificationUser(Guid id) : IEmailVerificationUser
    {
        public Guid Id { get; } = id;

        public int Confirmations { get; private set; }

        public void ConfirmEmail(DateTimeOffset confirmedAtUtc) => Confirmations++;
    }
}
