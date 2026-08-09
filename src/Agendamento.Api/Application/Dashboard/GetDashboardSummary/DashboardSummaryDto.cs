namespace Agendamento.Api.Application.Dashboard.GetDashboardSummary;

public class ConversionMetricsDto
{
    public int TotalQuotations { get; set; }
    public int ApprovedQuotations { get; set; }
    public decimal ConversionRatePercentage { get; set; }
}

public class DashboardSummaryDto
{
    public decimal TotalReceivedAmount { get; set; }
    public decimal TotalReceivableAmount { get; set; }
    public int PendingWorkOrdersCount { get; set; }
    public ConversionMetricsDto ConversionMetrics { get; set; } = new();
}
