namespace Agendamento.Api.Application.Customers.UpdateCustomer;

public record UpdateCustomerCommand(
    Guid CustomerId,
    string Name,
    string Phone,
    string? Email,
    string? DocumentType,
    string? DocumentValue,
    string? Street,
    string? Number,
    string? Complement,
    string? Neighborhood,
    string? City,
    string? State,
    string? ZipCode);
