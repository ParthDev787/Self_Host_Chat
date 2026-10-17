namespace API.Entities;

public class UserPublicKey
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public required string Username { get; set; }
    public required string PublicKeyJwk { get; set; }
    public string? PreKeyJwk { get; set; }
    public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("o");
}
