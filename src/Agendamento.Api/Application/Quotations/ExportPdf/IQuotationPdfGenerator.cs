using Agendamento.Api.Application.Quotations.GetQuotations;

namespace Agendamento.Api.Application.Quotations.ExportPdf;

public interface IQuotationPdfGenerator
{
    Task<byte[]> GeneratePdfAsync(QuotationDetailDto quotation, CancellationToken cancellationToken = default);
}
