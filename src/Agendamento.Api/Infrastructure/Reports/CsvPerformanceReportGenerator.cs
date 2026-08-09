using System.Text;

namespace Agendamento.Api.Infrastructure.Reports;

public class CsvPerformanceReportGenerator : ICsvPerformanceReportGenerator
{
    public byte[] Generate(IEnumerable<PerformanceReportRow> rows)
    {
        var csvBuilder = new StringBuilder();
        
        // Header
        csvBuilder.AppendLine("Data,Tipo,Descricao,Cliente,Valor,Status");

        foreach (var row in rows)
        {
            var dateStr = row.Date.ToString("yyyy-MM-dd HH:mm");
            var typeStr = EscapeCsv(row.Type);
            var descStr = EscapeCsv(row.Description);
            var customerStr = EscapeCsv(row.CustomerName);
            var amountStr = row.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            var statusStr = EscapeCsv(row.Status);

            csvBuilder.AppendLine($"{dateStr},{typeStr},{descStr},{customerStr},{amountStr},{statusStr}");
        }

        return Encoding.UTF8.GetBytes(csvBuilder.ToString());
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        
        if (value.Contains(',') || value.Contains('\"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        
        return value;
    }
}
