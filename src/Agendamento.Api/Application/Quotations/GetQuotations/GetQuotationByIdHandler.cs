using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendamento.Api.Application.Quotations.GetQuotations;

public record QuotationDetailDto(
    Guid Id,
    string Code,
    Guid CustomerId,
    string CustomerName,
    DateTime IssueDate,
    DateTime? ValidUntil,
    QuotationStatus Status,
    string? Notes,
    string? PaymentTerms,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    DepositInfoDto? DepositInfo,
    List<QuotationItemDto> Items);

public record DepositInfoDto(
    DepositType Type,
    decimal Value,
    decimal RequiredAmount,
    decimal RemainingBalance,
    string? PaymentNotes,
    bool IsPaid);

public record QuotationItemDto(
    Guid Id,
    Guid? ServiceItemId,
    string ServiceName,
    string Unit,
    decimal UnitPrice,
    decimal Quantity,
    decimal DiscountAmount,
    decimal TotalPrice);

public class GetQuotationByIdHandler
{
    private readonly AgendamentoDbContext _dbContext;

    public GetQuotationByIdHandler(AgendamentoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<QuotationDetailDto?> HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quotation = await _dbContext.Quotations
            .Include(q => q.Customer)
            .Include(q => q.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (quotation == null)
            return null;

        DepositInfoDto? depositInfoDto = null;
        if (quotation.DepositInfo != null)
        {
            depositInfoDto = new DepositInfoDto(
                quotation.DepositInfo.Type,
                quotation.DepositInfo.Value,
                quotation.DepositInfo.RequiredAmount,
                quotation.DepositInfo.RemainingBalance,
                quotation.DepositInfo.PaymentNotes,
                quotation.DepositInfo.IsPaid);
        }

        var itemsDto = quotation.Items.Select(i => new QuotationItemDto(
            i.Id,
            i.ServiceItemId,
            i.ServiceName,
            i.Unit,
            i.UnitPrice,
            i.Quantity,
            i.DiscountAmount,
            i.TotalPrice)).ToList();

        return new QuotationDetailDto(
            quotation.Id,
            quotation.Code,
            quotation.CustomerId,
            quotation.Customer != null ? quotation.Customer.Name : "N/A",
            quotation.IssueDate,
            quotation.ValidUntil,
            quotation.Status,
            quotation.Notes,
            quotation.PaymentTerms,
            quotation.SubtotalAmount,
            quotation.DiscountAmount,
            quotation.TotalAmount,
            depositInfoDto,
            itemsDto);
    }
}
