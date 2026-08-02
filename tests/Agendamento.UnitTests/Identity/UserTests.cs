using Agendamento.Domain.Identity;
using Agendamento.Infrastructure.Identity;
using Xunit;

namespace Agendamento.UnitTests.Identity;

public sealed class UserTests
{
    [Fact(DisplayName = "CPF e e-mail normalizados identificam uma única pessoa @spec:AC-007")]
    public void Create_NormalizesCpfAndEmail_ForGlobalIdentity()
    {
        var passwords = new AspNetPasswordService();
        var user = User.Create(
            "123.456.789-09",
            "  ADMIN@Example.COM  ",
            passwords.Hash("uma-senha-segura"));

        Assert.Equal("12345678909", user.Cpf);
        Assert.Equal("admin@example.com", user.Email);
        Assert.Equal(UserStatus.PendingEmailVerification, user.Status);
    }

    [Fact(DisplayName = "Senhas nunca são recuperáveis @spec:AC-008 @principle:P-005")]
    public void Create_StoresOnlyAnIdentityPasswordHash()
    {
        const string password = "uma-senha-segura";
        var passwords = new AspNetPasswordService();

        var user = User.Create("123.456.789-09", "admin@example.com", passwords.Hash(password));

        Assert.NotEqual(password, user.PasswordHash);
        Assert.True(passwords.Verify(user.PasswordHash, password));
        Assert.Null(typeof(User).GetProperty("Password"));
    }
}
