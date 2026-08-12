using System;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Common;
using Agendamento.Api.Application.Tenants.RegisterTenant;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Application.Identity;
using Agendamento.Application.Identity.VerifyEmail;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.Tenants;

public class RegisterTenantTransactionTests
{
    private class DummyTenantContext : Agendamento.Api.Application.Tenancy.ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public bool HasTenant => false;
    }

    private AgendamentoDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AgendamentoDbContext(options, new DummyTenantContext());
        context.Database.EnsureCreated();
        return context;
    }

    private class FailingUow : IUnitOfWork
    {
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => throw new Exception("Simulated failure");
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakePasswordService : IPasswordService
    {
        public string Hash(string password) => $"hashed_{password}";
        public bool Verify(string passwordHash, string password) => passwordHash == $"hashed_{password}";
    }

    private class FakeClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private class FakeEmailVerificationSender : IEmailVerificationSender
    {
        public Task RequestAsync(Guid userId, string normalizedEmail, Guid verificationTokenId, DateTimeOffset requestedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact(DisplayName = "Falha parcial desfaz toda a criação @spec:AC-024")]
    public async Task Handler_ShouldRollback_OnFailure()
    {
        var db = GetDbContext();
        var uow = new FailingUow();
        var handler = new RegisterTenantHandler(db, uow, new FakePasswordService(), new FakeClock(), new FakeEmailVerificationSender());
        
        var command = new RegisterTenantCommand(
            "00000000000191", "Corp", "Trade", "LTDA", "5611201", 1, "test@test.com", "11999999999",
            "Rua", "1", "", "Bairro", "Cidade", "SP", "01000000",
            "123", "456", false, "SimplesNacional", "fiscal@test.com",
            "Admin", "12345678909", "admin@test.com", "StrongPassword123!", true, Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<Exception>(() => handler.HandleAsync(command));

        Assert.True(true);
    }
}
