using System.Net.Http.Json;
using Agendamento.Api.Domain.Appointments;
using Agendamento.Api.Features.WorkOrders;

namespace Agendamento.IntegrationTests.Features;

internal static class WorkOrderTestRequests
{
    internal static ScheduleWorkOrderRequest Schedule(Guid quotationId, params Guid[] people)
    {
        var start = new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero)
            .AddHours(BitConverter.ToUInt32(quotationId.ToByteArray()) % 50000);
        return new(AppointmentType.TechnicalVisit, start, start.AddHours(1), people, null, null);
    }

    internal static HttpRequestMessage OrderRequest(string path, Guid key, ScheduleWorkOrderRequest schedule)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(schedule) };
        request.Headers.Add("Idempotency-Key", key.ToString());
        return request;
    }

    internal static async Task<HttpResponseMessage> PostOrderAsync(this HttpClient client, string path,
        Guid key, ScheduleWorkOrderRequest schedule)
    {
        using var request = OrderRequest(path, key, schedule);
        return await client.SendAsync(request);
    }

    internal static Task<HttpResponseMessage> PostOrderAsync(this HttpClient client, string path,
        Guid quotationId, Guid person) => client.PostOrderAsync(path, quotationId, Schedule(quotationId, person));
}
