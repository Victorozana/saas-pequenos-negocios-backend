namespace Agendamento.Api.Infrastructure.Reports;

public class PerformanceReportRow
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public interface ICsvPerformanceReportGenerator
{
    byte[] Generate(IEnumerable<PerformanceReportRow> rows);
}
