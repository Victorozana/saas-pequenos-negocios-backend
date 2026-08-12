using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Common;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Domain.Identity;
using Agendamento.Domain.Tenants;
using Agendamento.Application.Identity;
using Agendamento.Application.Identity.VerifyEmail;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Tenants.RegisterTenant;

public class RegisterTenantHandler
{
    private readonly AgendamentoDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordService _passwordService;
    private readonly IClock _clock;
    private readonly IEmailVerificationSender _emailSender;

    public RegisterTenantHandler(
        AgendamentoDbContext db, 
        IUnitOfWork uow, 
        IPasswordService passwordService, 
        IClock clock,
        IEmailVerificationSender emailSender)
    {
        _db = db;
        _uow = uow;
        _passwordService = passwordService;
        _clock = clock;
        _emailSender = emailSender;
    }

    public async Task<string> HandleAsync(RegisterTenantCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            throw new ArgumentException("Idempotency-Key é obrigatória.");

        ValidatePassword(command.AdminPassword);

        var idempotency = await _db.Set<IdempotencyRecord>()
            .FirstOrDefaultAsync(i => i.Key == command.IdempotencyKey, cancellationToken);
            
        if (idempotency != null)
        {
            return string.Empty;
        }

        await _uow.BeginTransactionAsync(cancellationToken);

        try
        {
            var cnpj = Cnpj.Create(command.Cnpj);
            var address = TenantAddress.Create(
                command.AddressStreet, 
                command.AddressNumber, 
                command.AddressComplement, 
                command.AddressNeighborhood, 
                command.AddressCity, 
                command.AddressState, 
                command.AddressZipCode);

            var tenant = Tenant.Create(
                cnpj, 
                command.CompanyName, 
                command.TradeName, 
                command.LegalNature, 
                command.PrimaryCnae, 
                (BusinessCategory)command.CategoryId, 
                command.Email, 
                command.Phone, 
                address);

            var taxRegime = Enum.Parse<TaxRegime>(command.TaxRegime);
            var fiscal = TenantFiscalProfile.Create(
                tenant.Id,
                command.StateRegistration,
                command.MunicipalRegistration,
                command.IsTaxExempt,
                taxRegime,
                command.FiscalEmail);

            var passwordHash = _passwordService.Hash(command.AdminPassword);
            var user = User.Create(command.AdminName, command.AdminCpf, command.AdminEmail, passwordHash);
            
            var membership = TenantMembership.Create(tenant.Id, user.Id, "owner_admin", command.IsLegalRepresentative);

            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var rawToken = Convert.ToHexString(tokenBytes).ToLowerInvariant();
            var verificationToken = EmailVerificationToken.Create(user.Id, rawToken, _clock.UtcNow);

            _db.Set<IdempotencyRecord>().Add(new IdempotencyRecord { Key = command.IdempotencyKey, CreatedAt = DateTime.UtcNow });

            _db.Tenants.Add(tenant);
            _db.TenantFiscalProfiles.Add(fiscal);
            _db.Users.Add(user);
            _db.TenantMemberships.Add(membership);
            _db.EmailVerificationTokens.Add(verificationToken);

            await _emailSender.RequestAsync(
                user.Id,
                user.Email,
                verificationToken.Id,
                _clock.UtcNow,
                cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            await _uow.CommitAsync(cancellationToken);

            return rawToken;
        }
        catch
        {
            await _uow.RollbackAsync(cancellationToken);
            throw; 
        }
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("A senha é obrigatória.");
        }

        if (password.Length < 12)
        {
            throw new ArgumentException("A senha deve conter no mínimo 12 caracteres.");
        }

        if (!password.Any(char.IsUpper))
        {
            throw new ArgumentException("A senha deve conter pelo menos uma letra maiúscula.");
        }

        if (!password.Any(char.IsLower))
        {
            throw new ArgumentException("A senha deve conter pelo menos uma letra minúscula.");
        }

        if (!password.Any(char.IsDigit))
        {
            throw new ArgumentException("A senha deve conter pelo menos um número.");
        }

        var specialCharacters = @"!@#$%^&*()_+=\[{\]};:>|./?,-";
        if (!password.Any(c => specialCharacters.Contains(c)))
        {
            throw new ArgumentException("A senha deve conter pelo menos um caractere especial.");
        }
    }
}
