namespace API.DTOs;

// ── Connection Request ──
public record SendConnectionRequestDto(string FromUsername, string ToUsername, string? Message);

// ── Messages ──
public record SendMessageDto(
    string Sender,
    string Recipient,
    string? Text,
    string? Ciphertext = null,
    string? Iv = null,
    string? Mac = null,
    string? EphemeralPublicKey = null,
    int Sequence = 0
);

public record MarkReadDto(string User, string WithUser);

// ── Keys ──
public record PublicKeyDto(string Username, string PublicKeyJwk, string? PreKeyJwk);

// ── Presence ──
public record HeartbeatDto(string Username);
