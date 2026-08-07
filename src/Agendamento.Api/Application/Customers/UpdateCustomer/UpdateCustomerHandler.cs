using Agendamento.Api.Application.Common;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Customers.UpdateCustomer;

public class UpdateCustomerHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCustomerHandler(AgendamentoDbContext dbContext, ITenantContext tenantContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> HandleAsync(UpdateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new InvalidOperationException("Unauthenticated tenant context.");
        }

        var customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == command.CustomerId, cancellationToken);
        if (customer == null)
            return false;

        CustomerDocument? document = null;
        if (!string.IsNullOrWhiteSpace(command.DocumentValue) && !string.IsNullOrWhiteSpace(command.DocumentType))
        {
            document = CustomerDocument.Create(command.DocumentType, command.DocumentValue);
        }

        CustomerAddress? address = null;
        if (!string.IsNullOrWhiteSpace(command.Street))
        {
            address = CustomerAddress.Create(
                command.Street,
                command.Number ?? "",
                command.Complement,
                command.Neighborhood ?? "",
                command.City ?? "",
                command.State ?? "",
                command.ZipCode ?? "");
        }

        customer.Update(
            command.Name,
            command.Phone,
            command.Email,
            document,
            address);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
