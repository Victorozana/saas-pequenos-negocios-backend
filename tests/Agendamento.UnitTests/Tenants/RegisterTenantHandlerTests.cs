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

namespace Agendamento.UnitTests.Tenants;

public class RegisterTenantHandlerTests
{
    private AgendamentoDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AgendamentoDbContext(options, null!);
        context.Database.EnsureCreated();
        return context;
    }

    private RegisterTenantCommand CreateCommand()
    {
        return new RegisterTenantCommand(
            "00000000000191", "Corp", "Trade", "LTDA", "5611201", 1, "test@test.com", "11999999999",
            "Rua", "1", "", "Bairro", "Cidade", "SP", "01000000",
            "123", "456", false, "SimplesNacional", "fiscal@test.com",
            "Admin", "12345678909", "admin@test.com", "StrongPassword123!", true, Guid.NewGuid().ToString());
    }

    private class FakeUow : IUnitOfWork
    {
        public bool Began { get; private set; }
        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) { Began = true; return Task.CompletedTask; }
        public Task CommitAsync(CancellationToken cancellationToken = default) { Committed = true; return Task.CompletedTask; }
        public Task RollbackAsync(CancellationToken cancellationToken = default) { RolledBack = true; return Task.CompletedTask; }
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

    [Fact(DisplayName = "Repetição idempotente não duplica o cadastro @spec:AC-023")]
    public async Task Handler_ShouldBeIdempotent()
    {
        var db = GetDbContext();
        var uow = new FakeUow();
        var handler = new RegisterTenantHandler(db, uow, new FakePasswordService(), new FakeClock(), new FakeEmailVerificationSender());
        var command = CreateCommand();

        await handler.HandleAsync(command);
        var initialCount = await db.Tenants.CountAsync();

        // Repetir
        await handler.HandleAsync(command);
        var finalCount = await db.Tenants.CountAsync();

        Assert.Equal(1, initialCount);
        Assert.Equal(1, finalCount);
    }
}
