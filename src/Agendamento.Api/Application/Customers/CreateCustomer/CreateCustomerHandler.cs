using Agendamento.Api.Application.Common;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Infrastructure.Persistence;

namespace Agendamento.Api.Application.Customers.CreateCustomer;

public class CreateCustomerHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerHandler(AgendamentoDbContext dbContext, ITenantContext tenantContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> HandleAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new InvalidOperationException("Unauthenticated tenant context.");
        }

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

        var customer = Customer.Create(
            _tenantContext.TenantId,
            command.Name,
            command.Phone,
            command.Email,
            document,
            address);

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}
