using System.Net.Http.Json;
using Agendamento.Api.Application.Financial.CreatePayable;
using Agendamento.Api.Application.Financial.GetFinancialStatement;
using Agendamento.Api.Application.Financial.RegisterPayment;
using Agendamento.Api.Domain.Financial;
using Microsoft.Extensions.DependencyInjection;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.IntegrationTests.Infrastructure;
using Xunit;

namespace Agendamento.IntegrationTests.Financial;

public class FinancialApiTests : IClassFixture<AgendamentoApiFactory>
{
    private readonly AgendamentoApiFactory _factory;
    private readonly HttpClient _client;

    public FinancialApiTests(AgendamentoApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Can_Create_Payable_And_Register_Payment()
    {
        // Arrange
        var command = new CreatePayableCommand("Supplier A", "Energy Bill", 150.50m, DateTime.UtcNow.AddDays(10));

        // Act 1: Create Payable
        var response = await _client.PostAsJsonAsync("/api/v1/financial/payables", command);
        response.EnsureSuccessStatusCode();

        var location = response.Headers.Location;
        Assert.NotNull(location);

        var idStr = location.ToString().Split('/').Last();
        var payableId = Guid.Parse(idStr);

        // Act 2: Register Payment
        var paymentCommand = new RegisterPaymentCommand(150.50m, PaymentMethod.Pix, DateTime.UtcNow, "Paid via app");
        var payResponse = await _client.PostAsJsonAsync($"/api/v1/financial/payables/{payableId}/pay", paymentCommand);
        payResponse.EnsureSuccessStatusCode();

        // Assert
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var title = await dbContext.PayableTitles.FindAsync(payableId);
        
        Assert.NotNull(title);
        Assert.Equal(TransactionStatus.Paid, title.Status);
        Assert.Equal(0, title.BalanceDue);
    }
}
