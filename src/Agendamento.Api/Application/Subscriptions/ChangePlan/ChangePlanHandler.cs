using Agendamento.Api.Application.Common;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Subscriptions.ChangePlan;

public class ChangePlanCommand
{
    public Guid NewPlanId { get; set; }
}

public class ChangePlanHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ChangePlanHandler(AgendamentoDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task HandleAsync(ChangePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _dbContext.SaasPlans
            .FirstOrDefaultAsync(p => p.Id == request.NewPlanId, cancellationToken);

        if (plan == null)
            throw new Exception("Plano não encontrado.");

        var subscription = await _dbContext.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == _tenantContext.TenantId, cancellationToken);

        if (subscription == null)
            throw new Exception("Tenant não possui assinatura ativa.");

        // Aqui chamaria API do Gateway de pagamento para atualizar a assinatura externa (ex: Stripe, Pagar.me)
        // Para simplificar, estamos apenas atualizando nosso domínio.

        subscription.ChangePlan(request.NewPlanId);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
