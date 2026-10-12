using Agendamento.Api.Application.Financial.CreatePayable;
using Agendamento.Api.Application.Financial.GetFinancialStatement;
using Agendamento.Api.Application.Financial.RegisterPayment;
using Agendamento.Api.Infrastructure.Authorization;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Domain.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Features.Financial;

public static class FinancialEndpoints
{
    public static void MapFinancialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/financial").RequireAuthorization().WithTags("Financial");
        group.MapGet("/receivables", async (AgendamentoDbContext db, CancellationToken ct) =>
        {
            var titles = await db.ReceivableTitles.AsNoTracking().Include(t => t.Payments).ToListAsync(ct);
            return Results.Ok(titles.Select(t => new FinancialTitleResponse(t.Id, t.Description, t.OriginalAmount,
                t.PaidAmount, t.BalanceDue, t.DueDate, t.Status.ToString(), t.CustomerId, t.QuotationId)).ToArray());
        }).WithName("GetReceivables").RequirePermission(Permissions.FinancialRead).Produces<FinancialTitleResponse[]>();
        group.MapGet("/payables", async (AgendamentoDbContext db, CancellationToken ct) =>
        {
            var titles = await db.PayableTitles.AsNoTracking().Include(t => t.Payments).ToListAsync(ct);
            return Results.Ok(titles.Select(t => new FinancialTitleResponse(t.Id, t.Description, t.OriginalAmount,
                t.PaidAmount, t.BalanceDue, t.DueDate, t.Status.ToString(), null, null, t.SupplierName)).ToArray());
        }).WithName("GetPayables").RequirePermission(Permissions.FinancialRead).Produces<FinancialTitleResponse[]>();
        group.MapPost("/payables", async ([FromBody] CreatePayableCommand command, CreatePayableHandler handler, CancellationToken ct) =>
        {
            try
            {
                var id = await handler.HandleAsync(command, ct);
                return Results.Created($"/api/v1/financial/payables/{id}", new FinancialCreatedResponse(id));
            }
            catch (ArgumentException) { return InvalidPayment(); }
        }).WithName("CreatePayable").RequirePermission(Permissions.FinancialWrite).Produces<FinancialCreatedResponse>(201).ProducesProblem(400);
        group.MapPost("/receivables/{id:guid}/pay", async (Guid id, [FromBody] RegisterPaymentCommand command, RegisterReceivablePaymentHandler handler, CancellationToken ct) =>
        {
            try { return await handler.HandleAsync(id, command, ct) ? Results.Ok() : Results.NotFound(); }
            catch (ArgumentException) { return InvalidPayment(); }
            catch (InvalidOperationException) { return InvalidPayment(); }
        }).WithName("PayReceivable").RequirePermission(Permissions.FinancialWrite).Produces(200).Produces(404).ProducesProblem(400);
        group.MapPost("/payables/{id:guid}/pay", async (Guid id, [FromBody] RegisterPaymentCommand command, RegisterPayablePaymentHandler handler, CancellationToken ct) =>
        {
            try { return await handler.HandleAsync(id, command, ct) ? Results.Ok() : Results.NotFound(); }
            catch (ArgumentException) { return InvalidPayment(); }
            catch (InvalidOperationException) { return InvalidPayment(); }
        }).WithName("PayPayable").RequirePermission(Permissions.FinancialWrite).Produces(200).Produces(404).ProducesProblem(400);
        group.MapGet("/statement", async ([FromQuery] DateTime startDate, [FromQuery] DateTime endDate, GetFinancialStatementHandler handler, CancellationToken ct) =>
        {
            if (endDate < startDate) return Results.Problem(statusCode: 400, title: "O fim do período deve ser posterior ao início.");
            return Results.Ok(await handler.HandleAsync(startDate, endDate, ct));
        }).WithName("GetFinancialStatement").RequirePermission(Permissions.FinancialRead).Produces<FinancialStatementResult>().ProducesProblem(400);
    }
    private static IResult InvalidPayment() => Results.Problem(statusCode: 400,
        title: "Dados financeiros inválidos.", detail: "Confira descrição, valor positivo, saldo disponível e situação da conta.");
}
public record FinancialCreatedResponse(Guid Id);
public record FinancialTitleResponse(Guid Id, string Description, decimal OriginalAmount, decimal PaidAmount,
    decimal BalanceDue, DateTime DueDate, string Status, Guid? CustomerId, Guid? QuotationId, string? SupplierName = null);
