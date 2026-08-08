using Agendamento.Api.Application.Financial.CreatePayable;
using Agendamento.Api.Application.Financial.GetFinancialStatement;
using Agendamento.Api.Application.Financial.RegisterPayment;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Api.Domain.Financial;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Features.Financial;

public static class FinancialEndpoints
{
    public static void MapFinancialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/financial").RequireAuthorization().WithTags("Financial");

        // Receivables
        group.MapGet("/receivables", async (AgendamentoDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var receivables = await dbContext.ReceivableTitles.ToListAsync(cancellationToken);
            return Results.Ok(receivables);
        }).WithName("GetReceivables");

        group.MapPost("/receivables/{id:guid}/pay", async (Guid id, [FromBody] RegisterPaymentCommand command, RegisterReceivablePaymentHandler handler, CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, command, cancellationToken);
            return result ? Results.Ok() : Results.NotFound();
        }).WithName("PayReceivable");

        // Payables
        group.MapGet("/payables", async (AgendamentoDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var payables = await dbContext.PayableTitles.ToListAsync(cancellationToken);
            return Results.Ok(payables);
        }).WithName("GetPayables");

        group.MapPost("/payables", async ([FromBody] CreatePayableCommand command, CreatePayableHandler handler, CancellationToken cancellationToken) =>
        {
            var id = await handler.HandleAsync(command, cancellationToken);
            return Results.Created($"/api/v1/financial/payables/{id}", new { Id = id });
        }).WithName("CreatePayable");

        group.MapPost("/payables/{id:guid}/pay", async (Guid id, [FromBody] RegisterPaymentCommand command, RegisterPayablePaymentHandler handler, CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, command, cancellationToken);
            return result ? Results.Ok() : Results.NotFound();
        }).WithName("PayPayable");

        // Statement
        group.MapGet("/statement", async ([FromQuery] DateTime startDate, [FromQuery] DateTime endDate, GetFinancialStatementHandler handler, CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(startDate, endDate, cancellationToken);
            return Results.Ok(result);
        }).WithName("GetFinancialStatement");
    }
}
