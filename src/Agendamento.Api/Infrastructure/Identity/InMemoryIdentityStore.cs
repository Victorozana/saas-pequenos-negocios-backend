using Agendamento.Application.Identity.CreateSession;
using Agendamento.Application.Identity.VerifyEmail;
using Agendamento.Domain.Identity;
using Agendamento.Infrastructure.Email;
using Agendamento.Infrastructure.Persistence.Entities;

namespace Agendamento.Infrastructure.Identity;

/// <summary>
/// Initial adapter used until tenant registration introduces the shared persistence context.
/// Its seed methods are composition seams and are never exposed as HTTP inputs.
/// </summary>
public sealed class InMemoryIdentityStore :
    IIdentityAuthenticationStore,
    IEmailVerificationTokenRepository,
    IEmailVerificationUserRepository,
    IEmailOutbox
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, User> _users = [];
    private readonly Dictionary<string, EmailVerificationToken> _tokens = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ActiveTenantMembership> _memberships = [];
    private readonly List<EmailOutboxMessage> _outbox = [];

    public void Seed(User user, ActiveTenantMembership? membership = null)
    {
        lock (_lock)
        {
            _users[user.Id] = user;
            if (membership is not null)
            {
                _memberships[user.Id] = membership;
            }
        }
    }

    public void Seed(EmailVerificationToken token)
    {
        lock (_lock)
        {
            _tokens[token.Digest] = token;
        }
    }

    public Task<AuthenticationIdentity?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var user = _users.Values.SingleOrDefault(candidate => candidate.Email == normalizedEmail);
            if (user is null)
            {
                return Task.FromResult<AuthenticationIdentity?>(null);
            }

            _memberships.TryGetValue(user.Id, out var membership);
            return Task.FromResult<AuthenticationIdentity?>(new AuthenticationIdentity(
                user.Id,
                user.PasswordHash,
                user.EmailVerifiedAtUtc is not null,
                user.Status == UserStatus.Active,
                membership));
        }
    }

    public Task<EmailVerificationToken?> FindByDigestAsync(
        string digest,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _tokens.TryGetValue(digest, out var token);
            return Task.FromResult(token);
        }
    }

    public Task SaveAsync(EmailVerificationToken token, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _tokens[token.Digest] = token;
        }

        return Task.CompletedTask;
    }

    public Task<IEmailVerificationUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult<IEmailVerificationUser?>(
                _users.TryGetValue(id, out var user) ? new VerificationUser(user) : null);
        }
    }

    public Task SaveAsync(IEmailVerificationUser user, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EnqueueAsync(EmailOutboxMessage message, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _outbox.Add(message);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailOutboxMessage> Outbox
    {
        get
        {
            lock (_lock)
            {
                return _outbox.ToArray();
            }
        }
    }

    private sealed class VerificationUser(User user) : IEmailVerificationUser
    {
        public Guid Id => user.Id;

        public void ConfirmEmail(DateTimeOffset confirmedAtUtc) => user.ConfirmEmail(confirmedAtUtc);
    }
}
