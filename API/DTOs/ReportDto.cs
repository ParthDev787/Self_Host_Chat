using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace API.DTOs;

public class ReportSubmissionDto
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = "Bug"; // Bug, Suggestion, Question, Chat Issue, Other

    [Required]
    [MinLength(5)]
    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    public List<IFormFile>? Images { get; set; }

    public IFormFile? Video { get; set; }
}

public class ReportResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ReportId { get; set; }
}

