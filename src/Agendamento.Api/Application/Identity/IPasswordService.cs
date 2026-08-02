namespace Agendamento.Application.Identity;

/// <summary>Boundary around the stack's password hashing mechanism.</summary>
public interface IPasswordService
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}
