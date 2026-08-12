using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Agendamento.Api.Features.Tenants;
using Agendamento.Api.Features.Identity;
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
    [Fact(DisplayName = "Sequência completa de onboarding: Registro -> Confirmação de E-mail -> Login @spec:AC-021")]
    public async Task Full_Onboarding_Flow_Should_Succeed()
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
            builder.UseSetting("Environment", "Development");
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
        Assert.NotNull(responseBody.VerificationToken);
        var rawToken = responseBody.VerificationToken;

        Assert.True(registerResponse.Headers.Contains("X-Development-Verification-Token"));
        var headerToken = registerResponse.Headers.GetValues("X-Development-Verification-Token").GetEnumerator();
        headerToken.MoveNext();
        Assert.Equal(rawToken, headerToken.Current);

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
        Assert.Equal(email, userProfile.Email);
        Assert.Equal("Administrador da Silva", userProfile.Name);
    }
}
