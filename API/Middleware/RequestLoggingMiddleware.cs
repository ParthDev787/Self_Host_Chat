using System.Diagnostics;
using Serilog.Context;

namespace API.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var id = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                 ?? Guid.NewGuid().ToString("N");
        ctx.Response.Headers["X-Correlation-Id"] = id;

        using (LogContext.PushProperty("CorrelationId", id))
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("REQUEST {Method} {Path}{Query}",
                ctx.Request.Method, ctx.Request.Path, ctx.Request.QueryString);
            try
            {
                await _next(ctx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UNHANDLED ERROR {Path}", ctx.Request.Path);
                ctx.Response.StatusCode = 500;
            }
            finally
            {
                sw.Stop();
                _logger.LogInformation("RESPONSE {Status} in {Ms} ms",
                    ctx.Response.StatusCode, sw.ElapsedMilliseconds);
            }
        }
    }
}
