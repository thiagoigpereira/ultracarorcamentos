using System.Text.Json;
using Domain.Exceptions;

namespace Api.Middleware;

public sealed class BusinessExceptionHandler
{
    private readonly RequestDelegate _next;

    public BusinessExceptionHandler(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException exception)
        {
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                type = "https://httpstatuses.com/422",
                title = "Business rule violation",
                status = 422,
                code = exception.Code,
                detail = exception.Message,
                traceId = context.TraceIdentifier
            }));
        }
    }
}
