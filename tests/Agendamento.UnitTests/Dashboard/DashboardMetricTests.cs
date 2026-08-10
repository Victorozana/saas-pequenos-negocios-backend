using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agendamento.Api.Application.Dashboard.ExportPerformanceReport;
using Agendamento.Api.Application.Dashboard.GetDashboardSummary;
using Agendamento.Api.Application.Dashboard.GetUpcomingSchedule;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Appointments;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Financial;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Domain.WorkOrders;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Api.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.UnitTests.Dashboard;

public class DashboardMetricTests
{
    private class DummyTenantContext : ITenantContext
    {
        public DummyTenantContext(Guid tenantId)
        {
            TenantId = tenantId;
            HasTenant = true;
        }

        public Guid TenantId { get; }
        public bool HasTenant { get; }
    }

    private AgendamentoDbContext GetDbContext(Guid tenantId, string dbName)
    {
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new AgendamentoDbContext(options, new DummyTenantContext(tenantId));
        context.Database.EnsureCreated();
        return context;
    }

    [Fact(DisplayName = "Faturamento considera apenas títulos baixados/pagos @spec:AC-066")]
    public async Task DashboardSummary_IncludesOnlyPaidTitlesForRevenue_AC066()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var db = GetDbContext(tenantId, dbName);

        var titlePaid = ReceivableTitle.Create(tenantId, "T1", 100m, DateTime.UtcNow);
        titlePaid.RegisterPayment(100m, PaymentMethod.Pix, DateTime.UtcNow);

        var titlePending = ReceivableTitle.Create(tenantId, "T2", 200m, DateTime.UtcNow.AddDays(5));
        
        db.ReceivableTitles.Add(titlePaid);
        db.ReceivableTitles.Add(titlePending);
        await db.SaveChangesAsync();

        var queryDb = GetDbContext(tenantId, dbName);
        var handler = new GetDashboardSummaryHandler(queryDb);
        var result = await handler.HandleAsync(new GetDashboardSummaryQuery());

        Assert.Equal(100m, result.TotalReceivedAmount);
        Assert.Equal(200m, result.TotalReceivableAmount);
    }

    [Fact(DisplayName = "Cálculo da taxa de conversão considera apenas orçamentos finalizados @spec:AC-068")]
    public async Task DashboardSummary_CalculatesConversionRateCorrectly_AC068()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var db = GetDbContext(tenantId, dbName);

        var q1 = Quotation.Create(tenantId, "Q1", Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(5), null, null);
        q1.AddItem(QuotationItem.Create(Guid.NewGuid(), "Item", "Un", 1, 100, 0));
        q1.MarkAsPending();
        q1.Approve();
        
        var q2 = Quotation.Create(tenantId, "Q2", Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(5), null, null);
        q2.AddItem(QuotationItem.Create(Guid.NewGuid(), "Item", "Un", 1, 100, 0));
        q2.MarkAsPending();
        q2.Reject();

        var q3 = Quotation.Create(tenantId, "Q3", Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(5), null, null);

        db.Quotations.Add(q1);
        db.Quotations.Add(q2);
        db.Quotations.Add(q3);
        await db.SaveChangesAsync();

        var queryDb = GetDbContext(tenantId, dbName);
        var handler = new GetDashboardSummaryHandler(queryDb);
        var result = await handler.HandleAsync(new GetDashboardSummaryQuery());

        Assert.Equal(2, result.ConversionMetrics.TotalQuotations);
        Assert.Equal(1, result.ConversionMetrics.ApprovedQuotations);
        Assert.Equal(50.0m, result.ConversionMetrics.ConversionRatePercentage);
    }

    [Fact(DisplayName = "Relatório CSV gerado corretamente e permite filtros @spec:AC-069")]
    public async Task ExportReport_FiltersByDateRange_AC069()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var db = GetDbContext(tenantId, dbName);

        var pastDate = DateTime.UtcNow.AddDays(-10);
        var recentDate = DateTime.UtcNow.AddDays(-1);
        
        var customer = Customer.Create(tenantId, "Cust", "111", "email@c.com", null, null);
        db.Customers.Add(customer);

        var qOld = Quotation.Create(tenantId, "Q_OLD", customer.Id, pastDate, pastDate.AddDays(5), null, null);
        var qRecent = Quotation.Create(tenantId, "Q_RECENT", customer.Id, recentDate, recentDate.AddDays(5), null, null);
        db.Quotations.Add(qOld);
        db.Quotations.Add(qRecent);
        await db.SaveChangesAsync();

        var queryDb = GetDbContext(tenantId, dbName);
        var generator = new CsvPerformanceReportGenerator();
        var handler = new ExportPerformanceReportHandler(queryDb, generator);

        var request = new ExportPerformanceReportQuery 
        { 
            StartDate = DateTime.UtcNow.AddDays(-5), 
            EndDate = DateTime.UtcNow 
        };
        var csvBytes = await handler.HandleAsync(request);
        var csvString = System.Text.Encoding.UTF8.GetString(csvBytes);

        Assert.Contains("Q_RECENT", csvString);
        Assert.DoesNotContain("Q_OLD", csvString);
    }
    
    [Fact(DisplayName = "Consultas agregadas respondem em tempo aceitável @spec:AC-067")]
    public async Task DashboardQueries_AreOptimized_AC067()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var queryDb = GetDbContext(tenantId, dbName);
        var handler = new GetDashboardSummaryHandler(queryDb);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        await handler.HandleAsync(new GetDashboardSummaryQuery());
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds < 500, "Consulta deve responder rápido.");
    }
}
