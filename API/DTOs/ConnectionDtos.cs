using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

// ── Connection Request ──
public record SendConnectionRequestDto(
    [Required][MaxLength(50)] string FromUsername,
    [Required][MaxLength(250)] string ToUsername,
    [MaxLength(500)] string? Message
);

// ── Messages ──
public record SendMessageDto(
    [Required][MaxLength(50)] string Sender,
    [Required][MaxLength(50)] string Recipient,
    [MaxLength(10000)] string? Text,
    [MaxLength(50000)] string? Ciphertext = null,
    [MaxLength(200)] string? Iv = null,
    [MaxLength(200)] string? Mac = null,
    [MaxLength(2000)] string? EphemeralPublicKey = null,
    int Sequence = 0
);

public record MarkReadDto(
    [Required][MaxLength(50)] string User,
    [Required][MaxLength(50)] string WithUser
);

// ── Keys ──
public record PublicKeyDto(
    [Required][MaxLength(50)] string Username,
    [Required][MaxLength(3000)] string PublicKeyJwk,
    [MaxLength(3000)] string? PreKeyJwk
);

// ── Presence ──
public record HeartbeatDto(
    [Required][MaxLength(50)] string Username
);
