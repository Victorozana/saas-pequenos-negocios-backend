using System;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Common;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Domain.Identity;
using Agendamento.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Tenants.RegisterTenant;

public class RegisterTenantHandler
{
    private readonly AgendamentoDbContext _db;
    private readonly IUnitOfWork _uow;

    public RegisterTenantHandler(AgendamentoDbContext db, IUnitOfWork uow)
    {
        _db = db;
        _uow = uow;
    }

    public async Task HandleAsync(RegisterTenantCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            throw new ArgumentException("Idempotency-Key é obrigatória.");

        var idempotency = await _db.Set<IdempotencyRecord>()
            .FirstOrDefaultAsync(i => i.Key == command.IdempotencyKey, cancellationToken);
            
        if (idempotency != null)
        {
            return;
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

            var user = User.Create(command.AdminName, command.AdminCpf, command.AdminEmail, Guid.NewGuid().ToString());
            
            var membership = TenantMembership.Create(tenant.Id, user.Id, "owner_admin", command.IsLegalRepresentative);

            _db.Set<IdempotencyRecord>().Add(new IdempotencyRecord { Key = command.IdempotencyKey, CreatedAt = DateTime.UtcNow });

            _db.Tenants.Add(tenant);
            _db.TenantFiscalProfiles.Add(fiscal);
            _db.Users.Add(user);
            _db.TenantMemberships.Add(membership);

            await _db.SaveChangesAsync(cancellationToken);
            await _uow.CommitAsync(cancellationToken);
        }
        catch
        {
            await _uow.RollbackAsync(cancellationToken);
            throw; 
        }
    }
}
