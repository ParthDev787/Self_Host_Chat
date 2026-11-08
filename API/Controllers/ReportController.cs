using API.DTOs;
using API.Interface;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportController(IReportService reportService) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(35 * 1024 * 1024)] // 35 MB total request limit (for video + images)
    public async Task<IActionResult> SubmitReport([FromForm] ReportSubmissionDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await reportService.SubmitReportAsync(dto, clientIp);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}

