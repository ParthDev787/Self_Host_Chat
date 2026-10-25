using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Context;

namespace API.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    public record FrontendLog(string Level, string Message, string? CorrelationId, string? Url);

    [HttpPost]
    public IActionResult Post([FromBody] FrontendLog log)
    {
        using (LogContext.PushProperty("CorrelationId", log.CorrelationId ?? "none"))
        {
            var logger = Log.ForContext("Source", "Frontend");
            if (string.Equals(log.Level, "error", StringComparison.OrdinalIgnoreCase))
                logger.Error("{Message} | {Url}", log.Message, log.Url);
            else
                logger.Information("{Message} | {Url}", log.Message, log.Url);
        }
        return Ok();
    }
}
