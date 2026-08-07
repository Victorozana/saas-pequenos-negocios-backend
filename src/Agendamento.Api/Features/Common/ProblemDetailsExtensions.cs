using Microsoft.AspNetCore.Mvc;

namespace Agendamento.Api.Features.Common;

public static class ProblemDetailsExtensions
{
    public static ProblemDetails Create(string title, string detail)
    {
        return new ProblemDetails
        {
            Title = title,
            Detail = detail
        };
    }
}
