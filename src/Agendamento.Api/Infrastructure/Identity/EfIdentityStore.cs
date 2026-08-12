using System;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Application.Identity;
using Agendamento.Application.Identity.CreateSession;
using Agendamento.Application.Identity.VerifyEmail;
using Agendamento.Domain.Identity;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Infrastructure.Identity;

public sealed class EfIdentityStore(AgendamentoDbContext db) :
    IIdentityAuthenticationStore,
    IEmailVerificationTokenRepository,
    IEmailVerificationUserRepository
{
    public async Task<AuthenticationIdentity?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
            
        if (user is null)
        {
            return null;
        }

        var membership = await db.TenantMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == user.Id, cancellationToken);

        var activeMembership = membership is not null 
            ? new ActiveTenantMembership(membership.TenantId, membership.Role)
            : null;

        return new AuthenticationIdentity(
            user.Id,
            user.PasswordHash,
            user.EmailVerifiedAtUtc is not null,
            user.Status == UserStatus.Active,
            activeMembership);
    }
    
    public async Task<EmailVerificationToken?> FindByDigestAsync(string digest, CancellationToken cancellationToken)
    {
        return await db.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.Digest == digest, cancellationToken);
    }

    public async Task SaveAsync(EmailVerificationToken token, CancellationToken cancellationToken)
    {
        var exists = await db.EmailVerificationTokens.AnyAsync(t => t.Id == token.Id, cancellationToken);
        if (!exists)
        {
            db.EmailVerificationTokens.Add(token);
        }
        else
        {
            db.EmailVerificationTokens.Update(token);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEmailVerificationUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user is not null ? new VerificationUser(user, db) : null;
    }

    public async Task SaveAsync(IEmailVerificationUser user, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed class VerificationUser(User user, AgendamentoDbContext db) : IEmailVerificationUser
    {
        public Guid Id => user.Id;

        public void ConfirmEmail(DateTimeOffset confirmedAtUtc)
        {
            user.ConfirmEmail(confirmedAtUtc);
            db.Users.Update(user);
        }
    }
}
