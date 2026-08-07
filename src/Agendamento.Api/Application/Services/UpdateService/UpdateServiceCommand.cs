namespace Agendamento.Api.Application.Services.UpdateService;

public record UpdateServiceCommand(
    string Name,
    string? Description,
    string Unit,
    decimal BasePrice,
    bool IsActive);
