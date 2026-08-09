using Microsoft.EntityFrameworkCore;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Api.Infrastructure.Reports;

namespace Agendamento.Api.Application.Dashboard.ExportPerformanceReport;

public class ExportPerformanceReportHandler
{
    private readonly AgendamentoDbContext _dbContext;
    private readonly ICsvPerformanceReportGenerator _reportGenerator;

    public ExportPerformanceReportHandler(AgendamentoDbContext dbContext, ICsvPerformanceReportGenerator reportGenerator)
    {
        _dbContext = dbContext;
        _reportGenerator = reportGenerator;
    }

    public async Task<byte[]> HandleAsync(ExportPerformanceReportQuery request, CancellationToken cancellationToken = default)
    {
        // Nota: A geração de CSV em endpoint síncrono é para propósitos atuais, mas 
        // a melhor prática para o futuro seria enviar por Job de Background caso o volume de dados cresça.
        
        var rows = new List<PerformanceReportRow>();

        // 1. Quotations
        var quotationsQuery = _dbContext.Quotations.Include(q => q.Customer).AsNoTracking();
        if (request.StartDate.HasValue) quotationsQuery = quotationsQuery.Where(q => q.IssueDate >= request.StartDate.Value);
        if (request.EndDate.HasValue) quotationsQuery = quotationsQuery.Where(q => q.IssueDate <= request.EndDate.Value);
        
        var quotations = await quotationsQuery.ToListAsync(cancellationToken);
        
        rows.AddRange(quotations.Select(q => new PerformanceReportRow
        {
            Date = q.IssueDate,
            Type = "Orçamento",
            Description = $"Orçamento {q.Code}",
            CustomerName = q.Customer?.Name ?? "Desconhecido",
            Amount = q.TotalAmount,
            Status = q.Status.ToString()
        }));

        // 2. Receivable Titles
        var receivablesQuery = _dbContext.ReceivableTitles.Include(r => r.Customer).AsNoTracking();
        if (request.StartDate.HasValue) receivablesQuery = receivablesQuery.Where(r => r.DueDate >= request.StartDate.Value);
        if (request.EndDate.HasValue) receivablesQuery = receivablesQuery.Where(r => r.DueDate <= request.EndDate.Value);
        
        var receivables = await receivablesQuery.ToListAsync(cancellationToken);
        
        rows.AddRange(receivables.Select(r => new PerformanceReportRow
        {
            Date = r.DueDate,
            Type = "Recebimento",
            Description = r.Description,
            CustomerName = r.Customer?.Name ?? "Desconhecido",
            Amount = r.OriginalAmount,
            Status = r.Status.ToString()
        }));

        // Order by Date descending
        rows = rows.OrderByDescending(r => r.Date).ToList();

        var csvBytes = _reportGenerator.Generate(rows);

        return csvBytes;
    }
}
