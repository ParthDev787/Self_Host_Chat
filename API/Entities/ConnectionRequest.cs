namespace API.Entities;

public class ConnectionRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public required string FromUsername { get; set; }
    public required string ToUsername { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = "pending";
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");
}
