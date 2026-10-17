namespace API.Entities;

public class ChatMessageEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public required string Sender { get; set; }
    public required string Recipient { get; set; }
    public required string Text { get; set; }
    public string? Ciphertext { get; set; }
    public string? Iv { get; set; }
    public string? Mac { get; set; }
    public string? EphemeralPublicKey { get; set; }
    public int Sequence { get; set; }
    public bool IsDelivered { get; set; } = true;
    public bool IsRead { get; set; } = false;
    public string? ReadAt { get; set; }
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");
}
