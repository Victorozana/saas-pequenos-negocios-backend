using System;

namespace Agendamento.Api.Infrastructure.Persistence;

public class IdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
