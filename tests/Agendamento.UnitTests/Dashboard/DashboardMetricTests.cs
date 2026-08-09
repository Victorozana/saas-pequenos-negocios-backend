using Xunit;
using System.Text;
using Agendamento.Api.Infrastructure.Reports;

namespace Agendamento.UnitTests.Dashboard;

public class DashboardMetricTests
{
    [Fact]
    public void GenerateCsv_ShouldCreateValidCsvContent()
    {
        // Arrange
        var generator = new CsvPerformanceReportGenerator();
        var rows = new List<PerformanceReportRow>
        {
            new PerformanceReportRow { Date = new DateTime(2023, 1, 1, 10, 0, 0), Type = "Orçamento", Description = "Orçamento 1", CustomerName = "João Silva", Amount = 1500.50m, Status = "Approved" },
            new PerformanceReportRow { Date = new DateTime(2023, 1, 2, 14, 30, 0), Type = "Recebimento", Description = "Entrada, 1a parcela", CustomerName = "Maria Oliveira", Amount = 500.00m, Status = "Paid" }
        };

        // Act
        var result = generator.Generate(rows);
        var csvString = Encoding.UTF8.GetString(result);

        // Assert
        Assert.NotNull(csvString);
        Assert.Contains("Data,Tipo,Descricao,Cliente,Valor,Status", csvString);
        Assert.Contains("2023-01-01 10:00,Orçamento,Orçamento 1,João Silva,1500.50,Approved", csvString);
        Assert.Contains("2023-01-02 14:30,Recebimento,\"Entrada, 1a parcela\",Maria Oliveira,500.00,Paid", csvString);
    }
}
