using Agendamento.Api.Application.Quotations.ExportPdf;
using Agendamento.Api.Application.Quotations.GetQuotations;
using Agendamento.Api.Domain.Quotations;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Agendamento.Api.Infrastructure.Pdf;

public class QuestPdfQuotationGenerator : IQuotationPdfGenerator
{
    public Task<byte[]> GeneratePdfAsync(QuotationDetailDto quotation, CancellationToken cancellationToken = default)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Element(header => ComposeHeader(header, quotation));
                page.Content().Element(content => ComposeContent(content, quotation));
                page.Footer().Element(ComposeFooter);
            });
        });

        var pdfBytes = document.GeneratePdf();
        return Task.FromResult(pdfBytes);
    }

    private void ComposeHeader(IContainer container, QuotationDetailDto quotation)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("ORÇAMENTO COMERCIAL").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text($"Código: {quotation.Code}").FontSize(12).SemiBold();
                column.Item().Text($"Data de Emissão: {quotation.IssueDate:dd/MM/yyyy}");
                
                if (quotation.ValidUntil.HasValue)
                {
                    column.Item().Text($"Válido até: {quotation.ValidUntil.Value:dd/MM/yyyy}");
                }
            });

            row.RelativeItem().AlignRight().Column(column =>
            {
                column.Item().Text("Dados do Cliente").SemiBold();
                column.Item().Text(quotation.CustomerName);
            });
        });
    }

    private void ComposeContent(IContainer container, QuotationDetailDto quotation)
    {
        container.PaddingVertical(1, Unit.Centimetre).Column(column =>
        {
            column.Spacing(10);

            column.Item().Element(c => ComposeTable(c, quotation));
            column.Item().Element(c => ComposeTotals(c, quotation));
            
            if (quotation.DepositInfo != null)
            {
                column.Item().Element(c => ComposeDepositInfo(c, quotation.DepositInfo));
            }

            if (!string.IsNullOrWhiteSpace(quotation.Notes) || !string.IsNullOrWhiteSpace(quotation.PaymentTerms))
            {
                column.Item().Element(c => ComposeNotes(c, quotation));
            }
        });
    }

    private void ComposeTable(IContainer container, QuotationDetailDto quotation)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3); // Descrição
                columns.RelativeColumn();  // Unidade
                columns.RelativeColumn();  // Qtd
                columns.RelativeColumn();  // Preço Unit
                columns.RelativeColumn();  // Desconto
                columns.RelativeColumn();  // Total
            });

            table.Header(header =>
            {
                header.Cell().Element(CellStyle).Text("Serviço / Produto");
                header.Cell().Element(CellStyle).AlignRight().Text("Unid.");
                header.Cell().Element(CellStyle).AlignRight().Text("Qtd.");
                header.Cell().Element(CellStyle).AlignRight().Text("V. Unitário");
                header.Cell().Element(CellStyle).AlignRight().Text("Desconto");
                header.Cell().Element(CellStyle).AlignRight().Text("Total");

                static IContainer CellStyle(IContainer container)
                {
                    return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                }
            });

            foreach (var item in quotation.Items)
            {
                table.Cell().Element(CellStyle).Text(item.ServiceName);
                table.Cell().Element(CellStyle).AlignRight().Text(item.Unit);
                table.Cell().Element(CellStyle).AlignRight().Text(item.Quantity.ToString("F2"));
                table.Cell().Element(CellStyle).AlignRight().Text(item.UnitPrice.ToString("C"));
                table.Cell().Element(CellStyle).AlignRight().Text(item.DiscountAmount.ToString("C"));
                table.Cell().Element(CellStyle).AlignRight().Text(item.TotalPrice.ToString("C"));

                static IContainer CellStyle(IContainer container)
                {
                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                }
            }
        });
    }

    private void ComposeTotals(IContainer container, QuotationDetailDto quotation)
    {
        container.Row(row =>
        {
            row.RelativeItem(); // empty space
            row.RelativeItem().Column(column =>
            {
                column.Item().Row(r =>
                {
                    r.RelativeItem().Text("Subtotal:");
                    r.RelativeItem().AlignRight().Text(quotation.SubtotalAmount.ToString("C"));
                });
                
                if (quotation.DiscountAmount > 0)
                {
                    column.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Descontos:");
                        r.RelativeItem().AlignRight().Text($"- {quotation.DiscountAmount:C}");
                    });
                }
                
                column.Item().PaddingTop(5).BorderTop(1).BorderColor(Colors.Black).Row(r =>
                {
                    r.RelativeItem().Text("TOTAL GERAL:").SemiBold();
                    r.RelativeItem().AlignRight().Text(quotation.TotalAmount.ToString("C")).SemiBold();
                });
            });
        });
    }

    private void ComposeDepositInfo(IContainer container, DepositInfoDto deposit)
    {
        container.Background(Colors.Grey.Lighten4).Padding(10).Column(column =>
        {
            column.Item().Text("Condições de Pagamento - Sinal/Entrada").SemiBold().FontSize(12);
            column.Spacing(5);
            
            string depositLabel = deposit.Type == DepositType.Percentage 
                ? $"Entrada de {deposit.Value:F0}%" 
                : $"Entrada Fixa de {deposit.Value:C}";

            column.Item().Row(r =>
            {
                r.RelativeItem().Text("Valor do Sinal (Entrada Exigida):").SemiBold();
                r.RelativeItem().AlignRight().Text($"{depositLabel} -> {deposit.RequiredAmount:C}").SemiBold();
            });

            column.Item().Row(r =>
            {
                r.RelativeItem().Text("Saldo Restante (Pós-Sinal):").SemiBold();
                r.RelativeItem().AlignRight().Text(deposit.RemainingBalance.ToString("C")).SemiBold();
            });

            if (!string.IsNullOrWhiteSpace(deposit.PaymentNotes))
            {
                column.Item().PaddingTop(5).Text($"Obs: {deposit.PaymentNotes}").Italic();
            }
        });
    }

    private void ComposeNotes(IContainer container, QuotationDetailDto quotation)
    {
        container.Column(column =>
        {
            if (!string.IsNullOrWhiteSpace(quotation.Notes))
            {
                column.Item().PaddingTop(10).Text("Observações:").SemiBold();
                column.Item().Text(quotation.Notes);
            }

            if (!string.IsNullOrWhiteSpace(quotation.PaymentTerms))
            {
                column.Item().PaddingTop(10).Text("Termos e Condições:").SemiBold();
                column.Item().Text(quotation.PaymentTerms);
            }
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(x =>
        {
            x.Span("Página ");
            x.CurrentPageNumber();
            x.Span(" de ");
            x.TotalPages();
        });
    }
}
