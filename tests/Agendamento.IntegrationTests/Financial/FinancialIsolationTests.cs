using System;
using System.Linq;
using System.Threading.Tasks;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Financial;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.Financial;

public class FinancialIsolationTests
{
    private class DummyTenantContext : ITenantContext
    {
        public DummyTenantContext(Guid tenantId)
        {
            TenantId = tenantId;
            HasTenant = true;
        }

        public Guid TenantId { get; }
        public bool HasTenant { get; }
    }

    private AgendamentoDbContext GetDbContext(Guid tenantId, string dbName)
    {
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new AgendamentoDbContext(options, new DummyTenantContext(tenantId));
        context.Database.EnsureCreated();
        return context;
    }

    [Fact(DisplayName = "Isolamento RLS: Contas a Receber e Pagar são restritas ao Tenant autenticado @spec:AC-055")]
    public async Task FinancialTitles_ShouldBeIsolated_ByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        
        var dbA = GetDbContext(tenantA, dbName);
        var recA = ReceivableTitle.Create(tenantA, "Rec A", 100m, DateTime.UtcNow);
        var payA = PayableTitle.Create(tenantA, "Sup A", "Pay A", 100m, DateTime.UtcNow);
        dbA.ReceivableTitles.Add(recA);
        dbA.PayableTitles.Add(payA);
        await dbA.SaveChangesAsync();

        var dbB = GetDbContext(tenantB, dbName);
        var recB = ReceivableTitle.Create(tenantB, "Rec B", 200m, DateTime.UtcNow);
        var payB = PayableTitle.Create(tenantB, "Sup B", "Pay B", 200m, DateTime.UtcNow);
        dbB.ReceivableTitles.Add(recB);
        dbB.PayableTitles.Add(payB);
        await dbB.SaveChangesAsync();

        var queryA = GetDbContext(tenantA, dbName);
        var recsA = await queryA.ReceivableTitles.ToListAsync();
        var paysA = await queryA.PayableTitles.ToListAsync();

        Assert.Single(recsA);
        Assert.Single(paysA);
        Assert.Equal("Rec A", recsA.First().Description);
        Assert.Equal("Pay A", paysA.First().Description);

        var queryB = GetDbContext(tenantB, dbName);
        var recsB = await queryB.ReceivableTitles.ToListAsync();
        var paysB = await queryB.PayableTitles.ToListAsync();

        Assert.Single(recsB);
        Assert.Single(paysB);
        Assert.Equal("Rec B", recsB.First().Description);
        Assert.Equal("Pay B", paysB.First().Description);
    }
}
