using Agendamento.Api.Domain.Notifications;
using Xunit;

namespace Agendamento.UnitTests.Notifications;

public class NotificationDomainTests
{
    [Fact]
    public void NotificationTemplate_Should_Render_Correctly_With_Variables()
    {
        // Arrange
        var templateString = "Olá {{Nome}}, seu agendamento para {{Data}} está confirmado.";
        var variables = new Dictionary<string, string>
        {
            { "Nome", "Victor" },
            { "Data", "10/10/2026" }
        };
        var template = new NotificationTemplate("Test", templateString, variables);

        // Act
        var result = template.Render();

        // Assert
        Assert.Equal("Olá Victor, seu agendamento para 10/10/2026 está confirmado.", result);
    }

    [Fact]
    public void NotificationMessage_Create_Should_Start_With_Pending_Status()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        
        // Act
        var notification = NotificationMessage.Create(tenantId, "5511999999999", NotificationChannel.WhatsApp, "Teste");

        // Assert
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(0, notification.RetryCount);
    }

    [Fact]
    public void NotificationMessage_MarkAsFailed_Should_Increase_RetryCount_And_Fail_After_3()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var notification = NotificationMessage.Create(tenantId, "5511999999999", NotificationChannel.WhatsApp, "Teste");

        // Act
        notification.MarkAsFailed("Erro 1");
        
        // Assert
        Assert.Equal(1, notification.RetryCount);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        
        // Act 2
        notification.MarkAsFailed("Erro 2");
        notification.MarkAsFailed("Erro 3");
        
        // Assert
        Assert.Equal(3, notification.RetryCount);
        Assert.Equal(NotificationStatus.Failed, notification.Status);
    }
}
