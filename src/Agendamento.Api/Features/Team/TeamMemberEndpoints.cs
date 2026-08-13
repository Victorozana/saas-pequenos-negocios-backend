using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Identity;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Notifications;
using Agendamento.Api.Infrastructure.Authorization;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Application.Identity;
using Agendamento.Domain.Identity;
using Agendamento.Domain.Tenants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Features.Team;

public static class TeamMemberEndpoints
{
    public static void MapTeamMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/team-members")
            .WithTags("Team")
            .RequireAuthorization();

        // AC-072: Listar membros do tenant atual com isolamento estrito
        group.MapGet("", async (
            AgendamentoDbContext db,
            ITenantContext tenantContext,
            CancellationToken cancellationToken) =>
        {
            var members = await db.TenantMemberships
                .Where(m => m.TenantId == tenantContext.TenantId)
                .Join(db.Users,
                    m => m.UserId,
                    u => u.Id,
                    (m, u) => new TeamMemberResponse(
                        u.Id,
                        u.Name,
                        u.Email,
                        m.Role,
                        m.IsLegalRepresentative,
                        m.Permissions.ToArray(),
                        u.Status.ToString(),
                        u.CreatedAtUtc
                    ))
                .ToListAsync(cancellationToken);

            return Results.Ok(members);
        })
        .WithName("GetTeamMembers")
        .RequirePermission("team.read");

        // AC-073: Atualizar papel/status do membro prevenindo desativação do último admin
        group.MapPut("/{userId:guid}", async (
            [FromRoute] Guid userId,
            [FromBody] UpdateTeamMemberRequest request,
            AgendamentoDbContext db,
            ITenantContext tenantContext,
            CancellationToken cancellationToken) =>
        {
            var membership = await db.TenantMemberships
                .FirstOrDefaultAsync(m => m.TenantId == tenantContext.TenantId && m.UserId == userId, cancellationToken);

            if (membership == null)
            {
                return Results.NotFound();
            }

            // Checagem AC-073: Trava do último administrador
            var isCurrentAdmin = membership.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
            var isChangingFromAdmin = isCurrentAdmin && !request.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);

            if (isChangingFromAdmin)
            {
                var activeAdminsCount = await db.TenantMemberships
                    .Where(m => m.TenantId == tenantContext.TenantId && m.Role.ToLower() == "admin")
                    .Join(db.Users.Where(u => u.Status == UserStatus.Active),
                        m => m.UserId,
                        u => u.Id,
                        (m, u) => m)
                    .CountAsync(cancellationToken);

                if (activeAdminsCount <= 1)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Conflict",
                        detail: "O tenant não pode ficar sem nenhum administrador ativo."
                    );
                }
            }

            membership.UpdateRoleAndPermissions(request.Role, request.Permissions ?? new List<string>());
            await db.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .WithName("UpdateTeamMember")
        .RequirePermission("team.write");

        // AC-074: Envio de convite único e expirável em 7 dias
        group.MapPost("/invitations", async (
            [FromBody] SendInvitationRequest request,
            AgendamentoDbContext db,
            ITenantContext tenantContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.BadRequest("O e-mail é obrigatório.");
            }

            var invitation = TeamInvitation.Create(
                tenantContext.TenantId,
                request.Email,
                request.Role ?? "Employee",
                request.Permissions ?? new List<string>(),
                expirationDays: 7
            );

            db.TeamInvitations.Add(invitation);

            // Adiciona mensagem na Outbox para envio do e-mail
            var notification = NotificationMessage.Create(
                tenantContext.TenantId,
                invitation.Email,
                NotificationChannel.Email,
                $"Você foi convidado para a equipe. Use o token para aceitar: {invitation.Token}"
            );
            db.NotificationMessages.Add(notification);

            await db.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/v1/team-members/invitations/{invitation.Id}", new
            {
                invitation.Id,
                invitation.Email,
                invitation.Role,
                invitation.Token,
                invitation.ExpiresAtUtc,
                invitation.Status
            });
        })
        .WithName("SendTeamInvitation")
        .RequirePermission("team.write");

        // AC-075: Aceite de convite e definição de senha
        app.MapPost("/api/v1/team-members/invitations/accept", async (
            [FromBody] AcceptInvitationRequest request,
            AgendamentoDbContext db,
            IPasswordService passwordService,
            CancellationToken cancellationToken) =>
        {
            var invitation = await db.TeamInvitations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Token == request.Token, cancellationToken);

            if (invitation == null || invitation.Status != TeamInvitationStatus.Pending)
            {
                return Results.BadRequest("Convite inválido ou já utilizado.");
            }

            if (DateTimeOffset.UtcNow > invitation.ExpiresAtUtc)
            {
                return Results.BadRequest("O convite está expirado.");
            }

            invitation.Accept();

            var existingUser = await db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == invitation.Email, cancellationToken);

            User user;
            if (existingUser != null)
            {
                user = existingUser;
            }
            else
            {
                var passwordHash = passwordService.Hash(request.Password);
                user = User.Create(
                    name: request.Name ?? invitation.Email.Split('@')[0],
                    cpf: null,
                    email: invitation.Email,
                    passwordHash: passwordHash
                );
                user.ConfirmEmail(DateTimeOffset.UtcNow);
                db.Users.Add(user);
            }

            var membership = TenantMembership.Create(
                invitation.TenantId,
                user.Id,
                invitation.Role,
                isLegalRepresentative: false,
                permissions: invitation.Permissions
            );

            db.TenantMemberships.Add(membership);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { Message = "Convite aceito com sucesso." });
        })
        .AllowAnonymous()
        .WithTags("Team")
        .WithName("AcceptTeamInvitation");
    }
}

public record TeamMemberResponse(
    Guid UserId,
    string Name,
    string Email,
    string Role,
    bool IsLegalRepresentative,
    string[] Permissions,
    string Status,
    DateTimeOffset CreatedAtUtc
);

public record UpdateTeamMemberRequest(
    string Role,
    List<string>? Permissions
);

public record SendInvitationRequest(
    string Email,
    string Role,
    List<string>? Permissions
);

public record AcceptInvitationRequest(
    string Token,
    string Password,
    string? Name
);
