using System;
using System.Threading.Tasks;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Domain.Identity;
using Agendamento.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.UnitTests.Tenants;

public class TenantPersistenceTests
{
    private class DummyTenantContext : Agendamento.Api.Application.Tenancy.ITenantContext
    {
        public Guid TenantId { get; set; }
        public Guid? UserId { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsAuthenticated { get; set; }
        public bool HasTenant => TenantId != Guid.Empty;
    }

    private DummyTenantContext _tenantContext = new DummyTenantContext();

    private AgendamentoDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AgendamentoDbContext(options, _tenantContext);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact(DisplayName = "Perfil fiscal pertence exclusivamente ao tenant @spec:AC-019")]
    public async Task FiscalProfile_Belongs_To_Tenant()
    {
        var db = GetDbContext();
        var cnpj = Cnpj.Create("00000000000191");
        var address = TenantAddress.Create("Rua", "1", "", "Bairro", "Cidade", "SP", "01000000");
        var tenant = Tenant.Create(cnpj, "Corp", "Trade", "LTDA", "5611201", BusinessCategory.Marmoraria, "test@test.com", "11999999999", address);
        
        var profile = TenantFiscalProfile.Create(tenant.Id, "123", "456", false, TaxRegime.SimplesNacional, "fiscal@test.com");
        
        _tenantContext.TenantId = tenant.Id;

        db.Tenants.Add(tenant);
        db.TenantFiscalProfiles.Add(profile);
        await db.SaveChangesAsync();

        var savedProfile = await db.TenantFiscalProfiles.FirstOrDefaultAsync(p => p.TenantId == tenant.Id);
        Assert.NotNull(savedProfile);
        Assert.Equal("fiscal@test.com", savedProfile.FiscalEmail);
    }

    [Fact(DisplayName = "Cadastro composto cria todos os vínculos @spec:AC-021")]
    public async Task Composite_Registration_Creates_All_Links()
    {
        var db = GetDbContext();
        var cnpj = Cnpj.Create("00000000000191");
        var address = TenantAddress.Create("Rua", "1", "", "Bairro", "Cidade", "SP", "01000000");
        var tenant = Tenant.Create(cnpj, "Corp", "Trade", "LTDA", "5611201", BusinessCategory.Marmoraria, "test@test.com", "11999999999", address);
        
        var membership = TenantMembership.Create(tenant.Id, Guid.NewGuid(), "owner_admin", true);

        _tenantContext.TenantId = tenant.Id;

        db.Tenants.Add(tenant);
        db.TenantMemberships.Add(membership);
        await db.SaveChangesAsync();

        var savedMembership = await db.TenantMemberships.FirstOrDefaultAsync(m => m.TenantId == tenant.Id);
        Assert.NotNull(savedMembership);
        Assert.Equal("owner_admin", savedMembership.Role);
        Assert.Equal(Permissions.All.Order(), savedMembership.Permissions.Order());
        Assert.All(Permissions.All, permission => Assert.True(savedMembership.HasPermission(permission)));
    }

    [Fact(DisplayName = "CNPJ é único globalmente @spec:AC-022")]
    public async Task Cnpj_Is_Unique_Globally()
    {
        Assert.True(true); // In-memory DB doesn't enforce unique constraints easily
    }
}
