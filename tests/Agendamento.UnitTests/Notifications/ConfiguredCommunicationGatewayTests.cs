using Agendamento.Api.Domain.Notifications;
using Agendamento.Api.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Agendamento.UnitTests.Notifications;

public sealed class ConfiguredCommunicationGatewayTests
{
    [Fact]
    public async Task Pickup_delivers_locally_and_is_rejected_outside_development()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nexo-mail-test-" + Guid.NewGuid());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:Email:Provider"] = "Pickup",
            ["Notifications:Email:From"] = "sender@example.test",
            ["Notifications:Email:PickupDirectory"] = directory,
        }).Build();
        var message = NotificationMessage.Create(Guid.NewGuid(), "recipient@example.test", NotificationChannel.Email,
            "Mensagem de teste local");
        var environment = new TestEnvironment { EnvironmentName = Environments.Production };
        var gateway = new ConfiguredCommunicationGateway(configuration, environment, new MockCommunicationGateway(NullLogger<MockCommunicationGateway>.Instance));
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.SendAsync(message));
            Assert.False(Directory.Exists(directory));
            environment.EnvironmentName = Environments.Development;
            Assert.True(await gateway.SendAsync(message));
            Assert.Single(Directory.GetFiles(directory, "*.eml"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "GatewayTests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
