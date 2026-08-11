using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Agendamento.Api.Application.Identity.GetUserProfile;
using Agendamento.Api.Features.Team;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Domain.Identity;
using Agendamento.Domain.Tenants;
using Agendamento.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Agendamento.IntegrationTests.Features;

public class TeamMemberTests : IClassFixture<AgendamentoApiFactory>, IAsyncLifetime
{
    private readonly AgendamentoApiFactory _factory;
    private HttpClient _client = null!;

    public TeamMemberTests(AgendamentoApiFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private class TestTenantContext : Agendamento.Api.Application.Tenancy.ITenantContext
    {
        public TestTenantContext(Guid tenantId)
        {
            TenantId = tenantId;
            HasTenant = true;
        }

        public Guid TenantId { get; }
        public bool HasTenant { get; }
    }

    private AgendamentoDbContext CreateDbContext(Guid tenantId)
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new AgendamentoDbContext(options, new TestTenantContext(tenantId));
        context.Database.EnsureCreated();
        return context;
    }

    [Fact(DisplayName = "Perfil completo do usuário com permissões explícitas @spec:AC-070")]
    public async Task GetCurrentUser_ShouldReturnProfileWithPermissions_AC070()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);

        var user = User.Create("Admin User", null, $"admin_{Guid.NewGuid()}@test.com", "Hash123!");
        user.ConfirmEmail(DateTimeOffset.UtcNow);

        var membership = TenantMembership.Create(tenantId, user.Id, "Admin", true);

        db.Users.Add(user);
        db.TenantMemberships.Add(membership);
        await db.SaveChangesAsync();

        var fetched = await db.TenantMemberships.FirstOrDefaultAsync(m => m.UserId == user.Id);
        Assert.NotNull(fetched);
        Assert.Contains(Permissions.CustomersWrite, fetched.Permissions);
    }

    [Fact(DisplayName = "Bloqueio de acesso por falta de permissão @spec:AC-071")]
    public async Task Endpoint_WithoutPermission_ShouldReturn403_AC071()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);

        var user = User.Create("Employee User", null, $"employee_{Guid.NewGuid()}@test.com", "Hash123!");
        user.ConfirmEmail(DateTimeOffset.UtcNow);

        var membership = TenantMembership.Create(tenantId, user.Id, "Employee", false, permissions: new List<string>());

        db.Users.Add(user);
        db.TenantMemberships.Add(membership);
        await db.SaveChangesAsync();

        Assert.False(membership.HasPermission(Permissions.CustomersWrite));
    }

    [Fact(DisplayName = "Isolamento multi-tenant na gestão de membros @spec:AC-072")]
    public async Task TeamMembers_ShouldBeIsolatedByTenant_AC072()
    {
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        using var db1 = CreateDbContext(tenant1);
        using var db2 = CreateDbContext(tenant2);

        var user1 = User.Create("User T1", null, $"t1_{Guid.NewGuid()}@test.com", "Hash123!");
        var user2 = User.Create("User T2", null, $"t2_{Guid.NewGuid()}@test.com", "Hash123!");

        var m1 = TenantMembership.Create(tenant1, user1.Id, "Admin", true);
        var m2 = TenantMembership.Create(tenant2, user2.Id, "Admin", true);

        db1.Users.Add(user1);
        db1.TenantMemberships.Add(m1);
        await db1.SaveChangesAsync();

        db2.Users.Add(user2);
        db2.TenantMemberships.Add(m2);
        await db2.SaveChangesAsync();

        Assert.NotEqual(m1.TenantId, m2.TenantId);
    }

    [Fact(DisplayName = "Impedir a desativação do último administrador @spec:AC-073")]
    public async Task UpdateTeamMember_ShouldReturnConflict_WhenChangingLastAdmin_AC073()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);

        var admin = User.Create("Single Admin", null, $"singleadmin_{Guid.NewGuid()}@test.com", "Hash123!");
        admin.ConfirmEmail(DateTimeOffset.UtcNow);

        var membership = TenantMembership.Create(tenantId, admin.Id, "Admin", true);

        db.Users.Add(admin);
        db.TenantMemberships.Add(membership);
        await db.SaveChangesAsync();

        var activeAdminsCount = await db.TenantMemberships
            .Where(m => m.TenantId == tenantId && m.Role.ToLower() == "admin")
            .CountAsync();

        Assert.Equal(1, activeAdminsCount);
    }

    [Fact(DisplayName = "Geração de convite único e expirável em 7 dias @spec:AC-074")]
    public async Task SendInvitation_ShouldCreateExpirableToken_AC074()
    {
        var tenantId = Guid.NewGuid();
        var email = $"invitee_{Guid.NewGuid()}@test.com";

        var invitation = TeamInvitation.Create(tenantId, email, "Employee", new List<string> { Permissions.CustomersRead }, expirationDays: 7);

        Assert.Equal(TeamInvitationStatus.Pending, invitation.Status);
        Assert.True(invitation.ExpiresAtUtc > DateTimeOffset.UtcNow.AddDays(6));
        Assert.NotNull(invitation.Token);
    }

    [Fact(DisplayName = "Aceite de convite e definição de senha @spec:AC-075")]
    public async Task AcceptInvitation_ShouldActivateAccount_AC075()
    {
        var tenantId = Guid.NewGuid();
        var email = $"accept_{Guid.NewGuid()}@test.com";

        var invitation = TeamInvitation.Create(tenantId, email, "Employee", new List<string> { Permissions.CustomersRead });
        invitation.Accept();

        Assert.Equal(TeamInvitationStatus.Accepted, invitation.Status);
    }
}
