using System.Text.Json;
using API.Errors;

namespace API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            // Known / expected errors — log as Warning (not Error)
            _logger.LogWarning(ex, "API error on {Method} {Path}: {Message}",
                context.Request.Method, context.Request.Path, ex.Message);

            await WriteErrorResponseAsync(context, ex.StatusCode, ex.Message, ex.Details);
        }
        catch (Exception ex)
        {
            // Unexpected errors — log full stack trace as Error
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            var details = _env.IsDevelopment() ? ex.StackTrace : null;
            await WriteErrorResponseAsync(context, StatusCodes.Status500InternalServerError,
                "An unexpected error occurred", details);
        }
    }

    private static async Task WriteErrorResponseAsync(
        HttpContext context, int statusCode, string message, string? details)
    {
        // Guard against writing to an already-started response
        if (context.Response.HasStarted) return;

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var body = new ErrorResponse(statusCode, message, details);
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, _jsonOptions));
    }

    private record ErrorResponse(int StatusCode, string Error, string? Details);
}
