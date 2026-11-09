using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Serilog.Context;

namespace API.Controllers;

[EnableRateLimiting("AuthLimiter")]
[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    public record FrontendLog(string Level, string Message, string? CorrelationId, string? Url);

    [HttpPost]
    public IActionResult Post([FromBody] FrontendLog log)
    {
        var safeMessage = log.Message.Length > 1000 ? log.Message[..1000] : log.Message;
        var safeUrl = log.Url?.Length > 500 ? log.Url[..500] : log.Url;

        using (LogContext.PushProperty("CorrelationId", log.CorrelationId ?? "none"))
        {
            var logger = Log.ForContext("Source", "Frontend");
            if (string.Equals(log.Level, "error", StringComparison.OrdinalIgnoreCase))
                logger.Error("{Message} | {Url}", safeMessage, safeUrl);
            else
                logger.Information("{Message} | {Url}", safeMessage, safeUrl);
        }
        return Ok();
    }
}
