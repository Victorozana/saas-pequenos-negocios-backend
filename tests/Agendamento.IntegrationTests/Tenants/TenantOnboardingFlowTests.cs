using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Agendamento.Api.Features.Tenants;
using Agendamento.Api.Features.Identity;
using Agendamento.Domain.Identity;
using Agendamento.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.Tenants;

public sealed class TenantOnboardingFlowTests
{
    [Theory(DisplayName = "Onboarding completo sem token na resposta em qualquer ambiente @spec:AC-021 @spec:AC-038 @spec:AC-100 @principle:P-005")]
    [InlineData("Development")]
    [InlineData("Test")]
    [InlineData("Production")]
    public async Task Full_Onboarding_Flow_Should_Succeed(string environment)
    {
        var dbName = $"OnboardingFlow_{Guid.NewGuid():N}";
        using var factory = new AgendamentoApiFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddDbContext<Agendamento.Api.Infrastructure.Persistence.AgendamentoDbContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                });
            });
            builder.UseEnvironment(environment);
        });

        var client = factory.CreateClient();

        var cnpj = "00000000000191";
        var email = "admin@example.com";
        var password = "StrongPassword123!";
        
        var request = new RegisterTenantRequest(
            Cnpj: cnpj,
            CompanyName: "Empresa de Alimentos Ltda",
            TradeName: "Alimentos Show",
            LegalNature: "Sociedade Empresária Limitada",
            PrimaryCnae: "56.11-2-01",
            CategoryId: 10,
            Email: "empresa@example.com",
            Phone: "11987654321",
            AddressStreet: "Rua das Flores",
            AddressNumber: "123",
            AddressComplement: "Sala 2",
            AddressNeighborhood: "Centro",
            AddressCity: "São Paulo",
            AddressState: "SP",
            AddressZipCode: "01001-000",
            StateRegistration: "123.456.789.111",
            MunicipalRegistration: "12345678",
            IsTaxExempt: false,
            TaxRegime: "SimplesNacional",
            FiscalEmail: "fiscal@example.com",
            AdminName: "Administrador da Silva",
            AdminCpf: "12345678909",
            AdminEmail: email,
            AdminPassword: password,
            IsLegalRepresentative: true
        );

        var idempotencyKey = Guid.NewGuid().ToString();
        var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tenants")
        {
            Content = JsonContent.Create(request)
        };
        registerRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var registerResponse = await client.SendAsync(registerRequest);
        if (registerResponse.StatusCode != HttpStatusCode.Created)
        {
            var errorText = await registerResponse.Content.ReadAsStringAsync();
            Assert.Fail($"Post /tenants failed with status {registerResponse.StatusCode}. Error details: {errorText}");
        }

        var responseBody = await registerResponse.Content.ReadFromJsonAsync<RegisterTenantResponse>();
        Assert.NotNull(responseBody);
        Assert.Equal("Cadastro realizado com sucesso.", responseBody.Message);
        using var responseJson = System.Text.Json.JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "message" }, responseJson.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.False(registerResponse.Headers.Contains("X-Development-Verification-Token"));

        using var invalidRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tenants")
        {
            Content = JsonContent.Create(request with { TaxRegime = "Host=invalid;Password=placeholder" })
        };
        invalidRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var invalidResponse = await client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidResponse.StatusCode);
        using var problem = System.Text.Json.JsonDocument.Parse(await invalidResponse.Content.ReadAsStringAsync());
        Assert.Equal("Não foi possível concluir o cadastro. Confira os dados e tente novamente.", problem.RootElement.GetProperty("detail").GetString());

        // Controlled test delivery only; never recover a raw token from HTTP or persisted digest.
        var rawToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Agendamento.Api.Infrastructure.Persistence.AgendamentoDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            Assert.Single(await db.EmailVerificationTokens.Where(t => t.UserId == user.Id).ToListAsync());
            db.EmailVerificationTokens.Add(EmailVerificationToken.Create(user.Id, rawToken, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var confirmResponse = await client.PostAsJsonAsync("/api/v1/auth/email-verifications", new { token = rawToken });
        Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/sessions", new CreateSessionRequest(email, password));
        if (loginResponse.StatusCode != HttpStatusCode.OK)
        {
            var err = await loginResponse.Content.ReadAsStringAsync();
            Assert.Fail($"Login failed with status {loginResponse.StatusCode}. Error details: {err}");
        }

        var session = await loginResponse.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        Assert.NotNull(session.AccessToken);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);

        var meResponse = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var userProfile = await meResponse.Content.ReadFromJsonAsync<Agendamento.Api.Application.Identity.GetUserProfile.UserProfileResponse>();
        Assert.NotNull(userProfile);
        Assert.NotEqual(Guid.Empty, userProfile.Id);
        Assert.Equal(email, userProfile.Email);
        Assert.Equal("Administrador da Silva", userProfile.Name);
        Assert.Equal("owner_admin", userProfile.Role);
        Assert.NotEqual(Guid.Empty, userProfile.TenantId);
        Assert.Equal(Permissions.All.Order(), userProfile.Permissions.Order());
        Assert.Equal(UserStatus.Active.ToString(), userProfile.Status);
    }
}
