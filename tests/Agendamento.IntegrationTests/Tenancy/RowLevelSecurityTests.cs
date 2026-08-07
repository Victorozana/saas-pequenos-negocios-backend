using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Domain.Tenants;
using Agendamento.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Agendamento.IntegrationTests.Tenancy;

public class RowLevelSecurityTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public RowLevelSecurityTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private class FakeTenantContext : ITenantContext
    {
        public Guid TenantId { get; set; }
        public bool HasTenant { get; set; }
    }

    [Fact]
    public async Task Rls_EnforcesCompleteIsolation_BetweenTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // 1. Arrange Data in Tenant A
        var contextA = new FakeTenantContext { HasTenant = true, TenantId = tenantA };
        
        var optionsBuilder = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseNpgsql(_fixture.ConnectionString);
        
        using (var dbA = new AgendamentoDbContext(optionsBuilder.Options, contextA))
        {
            await dbA.Database.MigrateAsync(); // Ensure DB is migrated

            var uowA = new UnitOfWork(dbA);
            await uowA.BeginTransactionAsync();
            
            dbA.TenantFiscalProfiles.Add(TenantFiscalProfile.Create(tenantA, "111", "222", false, Agendamento.Domain.Tenants.TaxRegime.SimplesNacional, "a@a.com"));
            await dbA.SaveChangesAsync();
            await uowA.CommitAsync();
        }

        // 2. Arrange Data in Tenant B
        var contextB = new FakeTenantContext { HasTenant = true, TenantId = tenantB };
        using (var dbB = new AgendamentoDbContext(optionsBuilder.Options, contextB))
        {
            var uowB = new UnitOfWork(dbB);
            await uowB.BeginTransactionAsync();
            
            dbB.TenantFiscalProfiles.Add(TenantFiscalProfile.Create(tenantB, "333", "444", false, Agendamento.Domain.Tenants.TaxRegime.LucroPresumido, "b@b.com"));
            await dbB.SaveChangesAsync();
            await uowB.CommitAsync();
        }

        // 3. Act & Assert - Tenant A cannot read Tenant B's data
        using (var dbA2 = new AgendamentoDbContext(optionsBuilder.Options, contextA))
        {
            var uowA2 = new UnitOfWork(dbA2);
            await uowA2.BeginTransactionAsync();

            var fiscalProfiles = await dbA2.TenantFiscalProfiles.ToListAsync();
            
            Assert.Single(fiscalProfiles); // Should only see one profile
            Assert.Equal(tenantA, fiscalProfiles[0].TenantId);
            Assert.Equal("111", fiscalProfiles[0].StateRegistration);
            
            await uowA2.RollbackAsync();
        }
    }
}
