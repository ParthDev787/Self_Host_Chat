using API.DTOs;

namespace API.Interface;

public interface IReportService
{
    Task<ReportResponseDto> SubmitReportAsync(ReportSubmissionDto dto, string? clientIp);
}

