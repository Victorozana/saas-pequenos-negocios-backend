using System.Text;
using System.Net.Http.Json;
using Agendamento.Application.Identity;
using Agendamento.Application.Identity.CreateSession;
using Agendamento.Api.Features.Identity;
using Agendamento.Infrastructure.Identity;
using Agendamento.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Agendamento.IntegrationTests.Identity;

public sealed class SessionEndpointTests
{
    [Fact(DisplayName = "@spec:AC-012 endpoint blocks a session for an unverified email")]
    public async Task Unverified_email_cannot_create_a_session()
    {
        using var client = CreateClient(isEmailVerified: false, isActive: false);

        var outcome = await client.PostAsJsonAsync("/api/v1/auth/sessions", new { email = "admin@example.com", password = "senha-segura-12" });

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, outcome.StatusCode);
        Assert.Equal("application/problem+json", outcome.Content.Headers.ContentType!.MediaType);
    }

    [Fact(DisplayName = "@spec:AC-013 @principle:P-003 @principle:P-005 valid login emits only user_tenant_and_role")]
    public async Task Valid_login_emits_a_minimized_session_without_request_tenant_selection()
    {
        using var client = CreateClient(isEmailVerified: true, isActive: true);

        var response = await client.PostAsJsonAsync("/api/v1/auth/sessions", new
        {
            email = "admin@example.com",
            password = "senha-segura-12",
            tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        });
        var body = await response.Content.ReadFromJsonAsync<SessionResponse>();
        var payload = DecodePayload(body!.AccessToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("sub", payload, StringComparison.Ordinal);
        Assert.Contains("tenant_id", payload, StringComparison.Ordinal);
        Assert.Contains("d179ed2b-ca14-4507-8099-43d8183795a9", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", payload, StringComparison.Ordinal);
        Assert.Contains("role", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("cpf", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateClient(bool isEmailVerified, bool isActive)
    {
        var membership = new ActiveTenantMembership(Guid.Parse("d179ed2b-ca14-4507-8099-43d8183795a9"), "administrator");
        var identity = new AuthenticationIdentity(
            Guid.Parse("b5578db1-1d94-4255-a4af-a9d2a60ce61b"),
            "password-hash",
            isEmailVerified,
            isActive,
            membership);
        return new AgendamentoApiFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IIdentityAuthenticationStore>(new InMemoryIdentityStore(identity));
            services.AddSingleton<IPasswordService, AcceptingPasswordService>();
            services.AddSingleton<ISessionIssuer>(new JwtSessionIssuer(
                JwtSessionIssuerOptions.Create("a-test-only-signing-key-that-is-not-production")));
            services.AddTransient<CreateSessionHandler>();
        })).CreateClient();
    }

    private static string DecodePayload(string token)
    {
        var encoded = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        encoded = encoded.PadRight(encoded.Length + ((4 - encoded.Length % 4) % 4), '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
    }

    private sealed class InMemoryIdentityStore(AuthenticationIdentity identity) : IIdentityAuthenticationStore
    {
        public Task<AuthenticationIdentity?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult<AuthenticationIdentity?>(normalizedEmail == "admin@example.com" ? identity : null);
    }

    private sealed class AcceptingPasswordService : IPasswordService
    {
        public string Hash(string password) => "password-hash";

        public bool Verify(string passwordHash, string password) =>
            passwordHash == "password-hash" && password == "senha-segura-12";
    }
}
