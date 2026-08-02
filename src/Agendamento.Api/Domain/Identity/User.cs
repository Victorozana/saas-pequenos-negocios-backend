using System.Globalization;

namespace Agendamento.Domain.Identity;

public sealed class User
{
    private User()
    {
    }

    public Guid Id { get; private set; }

    public string Cpf { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? EmailVerifiedAtUtc { get; private set; }

    public static User Create(
        string cpf,
        string email,
        string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cpf);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Cpf = NormalizeCpf(cpf),
            Email = NormalizeEmail(email),
            PasswordHash = passwordHash,
            Status = UserStatus.PendingEmailVerification,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        return user;
    }

    public void ConfirmEmail(DateTimeOffset confirmedAtUtc)
    {
        if (EmailVerifiedAtUtc is not null)
        {
            return;
        }

        EmailVerifiedAtUtc = confirmedAtUtc;
        Status = UserStatus.Active;
    }

    public static string NormalizeCpf(string cpf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cpf);

        var normalized = new string(cpf.Where(char.IsDigit).ToArray());
        if (normalized.Length != 11)
        {
            throw new ArgumentException("O CPF deve conter 11 dígitos.", nameof(cpf));
        }

        return normalized;
    }

    public static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalized = email.Trim().ToLower(CultureInfo.InvariantCulture);
        if (!normalized.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("O e-mail deve ser válido.", nameof(email));
        }

        return normalized;
    }
}
