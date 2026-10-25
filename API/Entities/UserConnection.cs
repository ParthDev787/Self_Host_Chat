namespace API.Entities;

public class UserConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public required string User1 { get; set; }
    public required string User2 { get; set; }
    public string ConnectedAt { get; set; } = DateTime.UtcNow.ToString("o");
}
