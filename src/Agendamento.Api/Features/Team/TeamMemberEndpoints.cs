using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Notifications;
using Agendamento.Api.Infrastructure.Authorization;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Api.Infrastructure.Tenancy;
using Agendamento.Application.Identity;
using Agendamento.Domain.Identity;
using Agendamento.Domain.Tenants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Features.Team;

public static class TeamMemberEndpoints
{
    public static void MapTeamMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/team-members").WithTags("Team").RequireAuthorization();
        group.MapGet("", async (AgendamentoDbContext db, ITenantContext tenant, CancellationToken ct) =>
        {
            var members = await db.TenantMemberships.Where(m => m.TenantId == tenant.TenantId)
                .Join(db.Users, m => m.UserId, u => u.Id, (m,u) => new TeamMemberResponse(
                    u.Id, u.Name, u.Email, m.Role, m.IsLegalRepresentative, m.Permissions.ToArray(),
                    m.IsActive ? u.Status.ToString() : "Inactive", u.CreatedAtUtc, m.IsActive)).ToListAsync(ct);
            return Results.Ok(members);
        }).WithName("GetTeamMembers").RequirePermission(Permissions.TeamRead).Produces<List<TeamMemberResponse>>();

        group.MapPut("/{userId:guid}", (Guid userId, [FromBody] UpdateTeamMemberRequest request,
            AgendamentoDbContext db, ITenantContext tenant, CancellationToken ct) =>
            UpdateMember(userId, request.Role, request.Permissions, request.IsActive, db, tenant, ct))
            .WithName("UpdateTeamMember").RequirePermission(Permissions.TeamWrite).Produces(204).Produces(404).ProducesProblem(400).ProducesProblem(409);
        group.MapPatch("/{userId:guid}/status", (Guid userId, [FromBody] TeamMemberStatusRequest request,
            AgendamentoDbContext db, ITenantContext tenant, CancellationToken ct) =>
            UpdateMember(userId, null, null, request.IsActive, db, tenant, ct))
            .WithName("UpdateTeamMemberStatus").RequirePermission(Permissions.TeamWrite).Produces(204).Produces(404).ProducesProblem(409);

        group.MapPost("/invitations", async ([FromBody] SendInvitationRequest request,
            AgendamentoDbContext db, ITenantContext tenant, CancellationToken ct) =>
        {
            if (!ValidRole(request.Role) || !ValidPermissions(request.Permissions)) return InvalidRequest();
            string email;
            try { email = User.NormalizeEmail(request.Email); } catch (ArgumentException) { return InvalidRequest(); }
            var alreadyMember = await db.TenantMemberships.Join(db.Users, m=>m.UserId,u=>u.Id,(m,u)=>new { m.TenantId, u.Email })
                .AnyAsync(x=>x.TenantId == tenant.TenantId && x.Email == email,ct);
            if (alreadyMember) return Results.Problem(statusCode:409,title:"Este usuário já pertence à equipe.");
            var invitation = TeamInvitation.Create(tenant.TenantId, email, request.Role, request.Permissions ?? [], 7);
            db.TeamInvitations.Add(invitation);
            db.NotificationMessages.Add(NotificationMessage.Create(tenant.TenantId, invitation.Email, NotificationChannel.Email,
                $"Você foi convidado para a equipe. Use o token para aceitar: {invitation.Token}"));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/team-members/invitations/{invitation.Id}",
                new TeamInvitationResponse(invitation.Id, invitation.Email, invitation.Role, invitation.ExpiresAtUtc, invitation.Status.ToString()));
        }).WithName("SendTeamInvitation").RequirePermission(Permissions.TeamWrite)
            .Produces<TeamInvitationResponse>(201).ProducesProblem(400).ProducesProblem(409);

        app.MapPost("/api/v1/team-members/invitations/accept", async ([FromBody] AcceptInvitationRequest request,
            AgendamentoDbContext db, HttpTenantContext tenant, IPasswordService passwords, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12)
                return Results.Problem(statusCode:400,title:"Informe um convite válido e uma senha com pelo menos 12 caracteres.");
        using var administrative = db.Administrative.Enter();
            var invitation = await db.TeamInvitations.IgnoreQueryFilters().FirstOrDefaultAsync(i=>i.Token == request.Token,ct);
            if (invitation == null || invitation.Status != TeamInvitationStatus.Pending || invitation.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                return Results.Problem(statusCode:400,title:"Convite inválido, expirado ou já utilizado.");
            using var scope = tenant.UseValidatedInvitation(invitation);
            var user = await db.Users.FirstOrDefaultAsync(u=>u.Email == invitation.Email,ct);
            if (user != null && await db.TenantMemberships.AnyAsync(m=>m.UserId == user.Id && m.TenantId == invitation.TenantId,ct))
                return Results.Problem(statusCode:409,title:"Este usuário já pertence à equipe.");
            if (user == null)
            {
                user = User.Create(string.IsNullOrWhiteSpace(request.Name) ? invitation.Email.Split('@')[0] : request.Name, null,
                    invitation.Email, passwords.Hash(request.Password));
                user.ConfirmEmail(DateTimeOffset.UtcNow);
                db.Users.Add(user);
            }
            invitation.Accept();
            db.TenantMemberships.Add(TenantMembership.Create(invitation.TenantId, user.Id, invitation.Role, false, invitation.Permissions));
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { return Results.Problem(statusCode:409,title:"O convite já foi utilizado."); }
            catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            { return Results.Problem(statusCode:409,title:"O convite ou vínculo já foi utilizado."); }
            return Results.Ok(new TeamInvitationAcceptedResponse("Convite aceito com sucesso."));
        }).AllowAnonymous().WithTags("Team").WithName("AcceptTeamInvitation")
            .Produces<TeamInvitationAcceptedResponse>().ProducesProblem(400).ProducesProblem(409);
    }

    private static async Task<IResult> UpdateMember(Guid userId, string? role, List<string>? permissions, bool? isActive,
        AgendamentoDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (role != null && !ValidRole(role) || !ValidPermissions(permissions)) return InvalidRequest();
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (transaction != null)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({tenant.TenantId.ToString()}, 0))", ct);
        var membership = await db.TenantMemberships.FirstOrDefaultAsync(m=>m.TenantId == tenant.TenantId && m.UserId == userId,ct);
        if (membership == null) return Results.NotFound();
        if (membership.IsActive && TenantMembership.IsAdministrativeRole(membership.Role) &&
            (isActive == false || role != null && !TenantMembership.IsAdministrativeRole(role)))
        {
            var administrators = await db.TenantMemberships.Where(m=>m.TenantId == tenant.TenantId && m.IsActive)
                .Join(db.Users.Where(u=>u.Status == UserStatus.Active),m=>m.UserId,u=>u.Id,(m,u)=>m.Role).ToListAsync(ct);
            if (administrators.Count(TenantMembership.IsAdministrativeRole) <= 1)
                return Results.Problem(statusCode:409,title:"O tenant não pode ficar sem nenhum administrador ativo.");
        }
        if (role != null) membership.UpdateRoleAndPermissions(role, permissions ?? []);
        if (isActive.HasValue) membership.SetActive(isActive.Value);
        await db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return Results.NoContent();
    }
    private static bool ValidRole(string? role) => !string.IsNullOrWhiteSpace(role) && role.Length <= 50;
    private static bool ValidPermissions(List<string>? permissions) => permissions == null || permissions.All(p=>Permissions.All.Contains(p));
    private static IResult InvalidRequest() => Results.Problem(statusCode:400,title:"Informe e-mail, papel e permissões válidos.");
}
public record TeamMemberResponse(Guid UserId, string Name, string Email, string Role, bool IsLegalRepresentative,
    string[] Permissions, string Status, DateTimeOffset CreatedAtUtc, bool IsActive);
public record UpdateTeamMemberRequest(string Role, List<string>? Permissions, bool? IsActive = null);
public record TeamMemberStatusRequest(bool IsActive);
public record SendInvitationRequest(string Email, string Role, List<string>? Permissions);
public record AcceptInvitationRequest(string Token, string Password, string? Name);
public record TeamInvitationResponse(Guid Id, string Email, string Role, DateTimeOffset ExpiresAtUtc, string Status);
public record TeamInvitationAcceptedResponse(string Message);
