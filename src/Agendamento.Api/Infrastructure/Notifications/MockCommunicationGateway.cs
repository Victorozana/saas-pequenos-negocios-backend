using Agendamento.Api.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace Agendamento.Api.Infrastructure.Notifications;

public class MockCommunicationGateway : ICommunicationGateway
{
    private readonly ILogger<MockCommunicationGateway> _logger;

    public MockCommunicationGateway(ILogger<MockCommunicationGateway> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        // Simulating the actual sending process via external API (e.g., Twilio, SendGrid, etc.)
        _logger.LogInformation("--- SENDING NOTIFICATION ---");
        _logger.LogInformation($"To: {message.Recipient}");
        _logger.LogInformation($"Channel: {message.Channel}");
        _logger.LogInformation($"Content:\n{message.Content}");
        _logger.LogInformation("----------------------------");

        // Simulate success
        return Task.FromResult(true);
    }
}
