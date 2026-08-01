using Agendamento.Application.Identity;
using Microsoft.AspNetCore.Identity;

namespace Agendamento.Infrastructure.Identity;

public sealed class AspNetPasswordService : IPasswordService
{
    private readonly PasswordHasher<string> _hasher = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (password.Length < 12)
        {
            throw new ArgumentException("A senha deve ter ao menos 12 caracteres.", nameof(password));
        }

        return _hasher.HashPassword(string.Empty, password);
    }

    public bool Verify(string passwordHash, string password)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        return _hasher.VerifyHashedPassword(string.Empty, passwordHash, password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
