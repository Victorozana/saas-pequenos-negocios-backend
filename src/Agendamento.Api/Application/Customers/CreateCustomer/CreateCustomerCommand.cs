namespace Agendamento.Api.Application.Customers.CreateCustomer;

public record CreateCustomerCommand(
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
