namespace API.Entities;

public class IssueReport
{
    public string Id { get; set; } = string.Empty;
    public required string Username { get; set; }
    public required string Category { get; set; }
    public required string Description { get; set; }
    public string? ClientIp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int ImageCount { get; set; }
    public bool HasVideo { get; set; }
    public string? AttachmentNames { get; set; }
    public string Status { get; set; } = "Submitted";
    public string? ErrorMessage { get; set; }
}

