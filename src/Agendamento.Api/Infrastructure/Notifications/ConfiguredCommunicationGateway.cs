using System.Net;
using System.Net.Mail;
using System.Text;
using Agendamento.Api.Domain.Notifications;

namespace Agendamento.Api.Infrastructure.Notifications;

/// <summary>Email delivery for the outbox. Pickup is restricted to local development.</summary>
public sealed class ConfiguredCommunicationGateway(IConfiguration configuration, IHostEnvironment environment,
    MockCommunicationGateway mock) : ICommunicationGateway
{
    public async Task<bool> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var provider = configuration["Notifications:Email:Provider"] ?? "Mock";
        if (message.Channel != NotificationChannel.Email || provider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            return await mock.SendAsync(message, cancellationToken);

        using var client = new SmtpClient();
        var from = configuration["Notifications:Email:From"];
        if (string.IsNullOrWhiteSpace(from)) throw new InvalidOperationException("Email sender is not configured.");
        if (provider.Equals("Pickup", StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment()) throw new InvalidOperationException("Pickup is only allowed in Development.");
            var directory = configuration["Notifications:Email:PickupDirectory"];
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
                throw new InvalidOperationException("An absolute local pickup directory is required.");
            Directory.CreateDirectory(directory);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = directory;
        }
        else if (provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            client.Host = configuration["Notifications:Email:Host"] ?? throw new InvalidOperationException("SMTP host is required.");
            client.Port = configuration.GetValue("Notifications:Email:Port", 587);
            client.EnableSsl = configuration.GetValue("Notifications:Email:EnableSsl", true);
            client.UseDefaultCredentials = false;
            var username = configuration["Notifications:Email:Username"];
            if (!string.IsNullOrWhiteSpace(username)) client.Credentials = new NetworkCredential(username, configuration["Notifications:Email:Password"]);
        }
        else throw new InvalidOperationException("Unknown email provider.");

        using var mail = new MailMessage(from, message.Recipient, "Nexo Local — notificação", message.Content)
        { SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8 };
        await client.SendMailAsync(mail, cancellationToken);
        return true;
    }
}
