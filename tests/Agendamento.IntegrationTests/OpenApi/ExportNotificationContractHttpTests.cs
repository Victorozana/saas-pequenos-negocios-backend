using System.Net;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Quotations;
using Agendamento.IntegrationTests.Features;
using Xunit;

namespace Agendamento.IntegrationTests.OpenApi;

public sealed class ExportNotificationContractHttpTests : IClassFixture<TeamFinancialTestFactory>
{
    private readonly TeamFinancialTestFactory factory;
    public ExportNotificationContractHttpTests(TeamFinancialTestFactory factory) => this.factory = factory;

    [Fact(DisplayName = "Downloads PDF concorrentes preservam texto de cada orçamento e cliente @spec:AC-037 @spec:AC-047")]
    public async Task ConcurrentDownloadsPreserveEveryQuotation()
    {
        var (client, _, tenant) = await factory.Member("owner_admin", []);
        var quotations = new List<(Quotation Quotation, Customer Customer)>();
        using (var db = factory.Db(tenant))
        {
            for (var i = 0; i < 24; i++)
            {
                var customer = Customer.Create(tenant, $"Cliente concorrente {i:D2} fictício", "123", null, null, null);
                var quotation = Quotation.Create(tenant, "CONCURRENT-" + Guid.NewGuid().ToString("N"), customer.Id, DateTime.UtcNow.Date, null, null, null);
                quotation.AddItem(QuotationItem.Create(null, $"Serviço concorrente {i:D2}", "un", 100m + i, 1, 0));
                quotation.MarkAsPending();
                db.Customers.Add(customer); db.Quotations.Add(quotation); quotations.Add((quotation, customer));
            }
            await db.SaveChangesAsync();
        }
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloads = quotations.Select(async (pair, index) =>
        {
            await start.Task;
            using var response = await client.GetAsync($"/api/v1/quotations/{pair.Quotation.Id}/pdf");
            var bytes = await AssertDownload(response, "application/pdf", $"Orcamento-{pair.Quotation.Code}.pdf");
            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes));
            Assert.Contains("%%EOF", Encoding.ASCII.GetString(bytes));
            AssertPdfSemantics(bytes, pair.Quotation, pair.Customer, $"Serviço concorrente {index:D2}");
        }).ToArray();
        start.SetResult();
        await Task.WhenAll(downloads);
    }

    [Fact(DisplayName = "PDF e CSV provam mídia, bytes, download, contrato e isolamento @spec:AC-037")]
    public async Task DownloadsMatchHttpAndContract()
    {
        var (client, _, tenant) = await factory.Member("owner_admin", []);
        var (other, _, _) = await factory.Member("owner_admin", []);
        var customer = Customer.Create(tenant, "Cliente exportação fictício", "123", null, null, null);
        var quotation = Quotation.Create(tenant, "EXPORT-" + Guid.NewGuid().ToString("N"), customer.Id, DateTime.UtcNow.Date, null, null, null);
        quotation.AddItem(QuotationItem.Create(null, "Serviço exportado", "un", 123.45m, 1, 0));
        quotation.MarkAsPending();
        using (var db = factory.Db(tenant)) { db.Customers.Add(customer); db.Quotations.Add(quotation); await db.SaveChangesAsync(); }
        var contract = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
        var pdf = await client.GetAsync($"/api/v1/quotations/{quotation.Id}/pdf");
        var bytes = await AssertDownload(pdf, "application/pdf", $"Orcamento-{quotation.Code}.pdf");
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes));
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(bytes));
        Assert.True(bytes.Length > 1000);
        AssertPdfSemantics(bytes, quotation, customer, "Serviço exportado");
        AssertDownloadContract(contract, "/api/v1/quotations/{id}/pdf", "application/pdf", true);
        var forbiddenPdf = await other.GetAsync($"/api/v1/quotations/{quotation.Id}/pdf");
        await AssertEmpty(forbiddenPdf, HttpStatusCode.NotFound);
        var csv = await client.GetAsync("/api/v1/dashboard/report");
        var csvBytes = await AssertDownload(csv, "text/csv", null);
        Assert.Matches("^performance_report_[0-9]{14}\\.csv$", csv.Content.Headers.ContentDisposition!.FileNameStar!);
        var text = Encoding.UTF8.GetString(csvBytes);
        Assert.StartsWith("Data,Tipo,Descricao,Cliente,Valor,Status", text);
        Assert.Contains(quotation.Code, text); Assert.Contains(customer.Name, text); Assert.Contains("123.45", text);
        Assert.DoesNotContain(quotation.Code, await other.GetStringAsync("/api/v1/dashboard/report"));
        AssertDownloadContract(contract, "/api/v1/dashboard/report", "text/csv", false);
        var (limited, _, _) = await factory.Member("Employee", []);
        var forbiddenReport = await limited.GetAsync("/api/v1/dashboard/report");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenReport.StatusCode);
        Assert.Equal("application/problem+json", forbiddenReport.Content.Headers.ContentType!.MediaType);
        Assert.Equal(403, JsonNode.Parse(await forbiddenReport.Content.ReadAsStringAsync())!["status"]!.GetValue<int>());
        Assert.NotNull(contract["paths"]!["/api/v1/dashboard/report"]!["get"]!["responses"]!["403"]!["content"]!["application/problem+json"]);
        await AssertEmpty(await factory.CreateClient().GetAsync("/api/v1/dashboard/report"), HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Notificações e conversão retornam erros seguros alinhados ao contrato @spec:AC-037")]
    public async Task NotificationAndConversionErrorsMatchContract()
    {
        var (client, _, tenant) = await factory.Member("owner_admin", []);
        var (other, _, _) = await factory.Member("owner_admin", []);
        var customer = Customer.Create(tenant, "Cliente notificação fictício", "123", null, null, null);
        var quotation = Quotation.Create(tenant, "CONTRACT-" + Guid.NewGuid().ToString("N"), customer.Id, DateTime.UtcNow.Date, null, null, null);
        quotation.AddItem(QuotationItem.Create(null, "Serviço", "un", 100, 1, 0)); quotation.MarkAsPending();
        using (var db = factory.Db(tenant)) { db.Customers.Add(customer); db.Quotations.Add(quotation); await db.SaveChangesAsync(); }
        var contract = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
        foreach (var route in new[] { "schedule-reminder", "send-quotation-notification" })
        {
            var path = "/api/v1/notifications/" + route;
            await AssertProblem(await client.PostAsJsonAsync(path, new { appointmentId = Guid.NewGuid(), quotationId = Guid.NewGuid() }));
            AssertProblemContract(contract, path);
        }
        var notifyPath = "/api/v1/notifications/send-quotation-notification";
        var success = await client.PostAsJsonAsync(notifyPath, new { quotationId = quotation.Id });
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.Equal("application/json", success.Content.Headers.ContentType!.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(JsonNode.Parse(await success.Content.ReadAsStringAsync())!["message"]!.GetValue<string>()));
        await AssertProblem(await other.PostAsJsonAsync(notifyPath, new { quotationId = quotation.Id }));
        var conversionPath = $"/api/v1/quotations/{quotation.Id}/convert";
        await AssertProblem(await client.PostAsync(conversionPath, null));
        await AssertProblem(await other.PostAsync(conversionPath, null));
        AssertProblemContract(contract, "/api/v1/quotations/{id}/convert");
        await AssertProblem(await client.PatchAsJsonAsync($"/api/v1/quotations/{quotation.Id}/status", new { newStatus = 999 }));
        await AssertEmpty(await client.PatchAsJsonAsync($"/api/v1/quotations/{quotation.Id}/status", new { newStatus = 3 }), HttpStatusCode.NoContent);
        var converted = await client.PostAsync(conversionPath, null);
        Assert.Equal(HttpStatusCode.OK, converted.StatusCode);
        var orderId = JsonNode.Parse(await converted.Content.ReadAsStringAsync())!["workOrderId"]!.GetValue<Guid>();
        Assert.NotEqual(Guid.Empty, orderId);
        Assert.Equal("application/json", converted.Content.Headers.ContentType!.MediaType);
        var appointment = Agendamento.Api.Domain.Appointments.Appointment.Create(tenant, orderId,
            Agendamento.Api.Domain.Appointments.AppointmentType.Installation, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1), null, null, null);
        using (var db = factory.Db(tenant)) { db.Appointments.Add(appointment); await db.SaveChangesAsync(); }
        var reminder = await client.PostAsJsonAsync("/api/v1/notifications/schedule-reminder", new { appointmentId = appointment.Id });
        Assert.Equal(HttpStatusCode.OK, reminder.StatusCode);
        Assert.Equal("application/json", reminder.Content.Headers.ContentType!.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(JsonNode.Parse(await reminder.Content.ReadAsStringAsync())!["message"]!.GetValue<string>()));
        await AssertProblem(await other.PostAsJsonAsync("/api/v1/notifications/schedule-reminder", new { appointmentId = appointment.Id }));
        var invalidCreate = await client.PostAsJsonAsync("/api/v1/quotations", new { code = "INVALID", customerId = Guid.NewGuid(), issueDate = DateTime.UtcNow, items = Array.Empty<object>() });
        await AssertProblem(invalidCreate);
        var statusResponses = contract["paths"]!["/api/v1/quotations/{id}/status"]!["patch"]!["responses"]!;
        Assert.Null(statusResponses["204"]!["content"]); Assert.Null(statusResponses["404"]!["content"]);
    }

    private static async Task<byte[]> AssertDownload(HttpResponseMessage response, string media, string? fileName)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(media, response.Content.Headers.ContentType!.MediaType);
        var disposition = response.Content.Headers.ContentDisposition!;
        Assert.Equal("attachment", disposition.DispositionType);
        Assert.False(string.IsNullOrWhiteSpace(disposition.FileNameStar));
        if (fileName != null) Assert.Equal(fileName, disposition.FileNameStar);
        var bytes = await response.Content.ReadAsByteArrayAsync(); Assert.Equal(bytes.LongLength, response.Content.Headers.ContentLength);
        return bytes;
    }
    private static string NormalizeWhitespace(string value) => Regex.Replace(value, @"\s+", " ").Trim();
    private static string RemoveWhitespace(string value) => Regex.Replace(value, @"\s+", "");
    private static void AssertPdfSemantics(byte[] bytes, Quotation quotation, Customer customer, string service)
    {
        using var document = PdfDocument.Open(bytes);
        Assert.True(document.NumberOfPages > 0);
        var pdfText = NormalizeWhitespace(string.Join("\n", document.GetPages().Select(page => ContentOrderTextExtractor.GetText(page))));
        // Long identifiers wrap in the PDF; ignore only presentation whitespace.
        Assert.Contains(RemoveWhitespace(quotation.Code), RemoveWhitespace(pdfText));
        Assert.Contains(NormalizeWhitespace(customer.Name), pdfText);
        Assert.Contains(service, pdfText);
        Assert.Contains("TOTAL GERAL:", pdfText);
        Assert.Contains(RemoveWhitespace(quotation.TotalAmount.ToString("C", CultureInfo.CurrentCulture)), RemoveWhitespace(pdfText));
    }
    private static void AssertDownloadContract(JsonNode contract, string path, string media, bool binary)
    {
        var response = contract["paths"]![path]!["get"]!["responses"]!["200"]!;
        var schema = response["content"]![media]!["schema"]!;
        Assert.Equal("string", schema["type"]!.GetValue<string>());
        if (binary) Assert.Equal("binary", schema["format"]!.GetValue<string>());
        Assert.NotNull(response["headers"]!["Content-Disposition"]); Assert.NotNull(response["headers"]!["Content-Length"]);
    }
    private static void AssertProblemContract(JsonNode contract, string path) => Assert.NotNull(contract["paths"]![path]!["post"]!["responses"]!["400"]!["content"]!["application/problem+json"]!["schema"]);
    private static async Task AssertProblem(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadAsStringAsync(); var problem = JsonNode.Parse(body)!;
        Assert.Equal(400, problem["status"]!.GetValue<int>()); Assert.False(string.IsNullOrWhiteSpace(problem["title"]!.GetValue<string>()));
        Assert.NotNull(problem["type"]); Assert.Null(problem["error"]); Assert.Null(problem["message"]);
        Assert.DoesNotContain("InvalidOperationException", body); Assert.DoesNotContain("Unauthenticated tenant context", body);
    }
    private static async Task AssertEmpty(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode); Assert.Empty(await response.Content.ReadAsByteArrayAsync()); Assert.Null(response.Content.Headers.ContentType);
    }
}
