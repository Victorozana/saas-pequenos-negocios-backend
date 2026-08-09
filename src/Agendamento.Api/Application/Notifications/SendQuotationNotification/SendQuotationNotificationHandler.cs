using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Notifications;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Notifications.SendQuotationNotification;

public class SendQuotationNotificationHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public SendQuotationNotificationHandler(AgendamentoDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<bool> HandleAsync(SendQuotationNotificationCommand request, CancellationToken cancellationToken = default)
    {
        var quotation = await _dbContext.Quotations
            .Include(q => q.Customer)
            .FirstOrDefaultAsync(q => q.Id == request.QuotationId, cancellationToken);

        if (quotation == null)
            return false;

        var phone = quotation.Customer?.Phone;
        var email = quotation.Customer?.Email;

        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
            return false; // Nowhere to send

        var channel = !string.IsNullOrWhiteSpace(phone) ? NotificationChannel.WhatsApp : NotificationChannel.Email;
        var recipient = !string.IsNullOrWhiteSpace(phone) ? phone : email;

        var variables = new Dictionary<string, string>
        {
            { "NomeCliente", quotation.Customer?.Name ?? "Cliente" },
            { "NumeroOrcamento", quotation.Code },
            { "ValorTotal", quotation.TotalAmount.ToString("C") },
            { "DataValidade", quotation.ValidUntil?.ToString("dd/MM/yyyy") ?? "N/A" }
        };

        var template = new NotificationTemplate(
            "QuotationReady", 
            "Olá {{NomeCliente}}, o orçamento #{{NumeroOrcamento}} no valor de {{ValorTotal}} já está disponível e é válido até {{DataValidade}}.", 
            variables);

        var content = template.Render();

        var notification = NotificationMessage.Create(
            _tenantContext.TenantId,
            recipient!,
            channel,
            content);

        _dbContext.NotificationMessages.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
