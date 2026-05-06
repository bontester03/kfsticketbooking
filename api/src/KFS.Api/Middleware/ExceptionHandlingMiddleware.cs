using System.Text.Json;
using FluentValidation;
using KFS.Application.Common.Exceptions;

namespace KFS.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next; _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (ValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "_" : e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            await Write(context, 400, "validation_error", "Request validation failed.", errors);
        }
        catch (AppException ex)
        {
            await Write(context, ex.StatusCode, ex.Code, ex.Message, ex.Extra);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled");
            await Write(context, 500, "server_error", "An unexpected error occurred.");
        }
    }

    private static Task Write(HttpContext ctx, int status, string code, string message, object? extra = null)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        var payload = new { status, code, message, extra };
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
