using Agendamento.Api.Domain.Notifications;

namespace Agendamento.Api.Infrastructure.Notifications;

public interface ICommunicationGateway
{
    Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}
