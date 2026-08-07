namespace Agendamento.Api.Application.Services.CreateService;

public record CreateServiceCommand(
    string Name,
    string? Description,
    string Unit,
    decimal BasePrice);
