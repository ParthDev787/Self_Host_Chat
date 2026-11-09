using System.Security.Claims;
using API.DTOs;
using API.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;

[Authorize]
[ApiController]
[Route("api/connections")]
public class ConnectionsController(IConnectionService connectionService) : ControllerBase
{
    private string CurrentUsername => User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.ToLower() ?? "";

    // ── Unified Fast Sync (Reduces 4 API calls down to 1 single call) ──

    [HttpGet("sync")]
    public async Task<IActionResult> Sync([FromQuery] string? activeChat, [FromQuery] int limit = 30)
    {
        if (string.IsNullOrWhiteSpace(CurrentUsername)) return Unauthorized();

        var result = await connectionService.GetSyncDataAsync(CurrentUsername, activeChat, limit);
        return Ok(result);
    }

    // ── Connection Requests ──

    [HttpPost("request")]
    public async Task<IActionResult> SendRequest([FromBody] SendConnectionRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUsername)) return Unauthorized();

        // Enforce that sender cannot impersonate another user
        if (!string.Equals(CurrentUsername, dto.FromUsername?.Trim(), StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var result = await connectionService.SendRequestAsync(dto);
        return Ok(result);
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests([FromQuery] string? username)
    {
        if (string.IsNullOrWhiteSpace(CurrentUsername)) return Unauthorized();

        // Caller can only fetch their own connection requests
        var targetUser = string.IsNullOrWhiteSpace(username) ? CurrentUsername : username.Trim().ToLower();
        if (targetUser != CurrentUsername)
            return Forbid();

        var result = await connectionService.GetRequestsAsync(targetUser);
        return Ok(result);
    }

    [HttpPost("accept/{id}")]
    public async Task<IActionResult> AcceptRequest(string id)
    {
        if (string.IsNullOrWhiteSpace(CurrentUsername)) return Unauthorized();

        var result = await connectionService.AcceptRequestAsync(id, CurrentUsername);
        return Ok(result);
    }

    [HttpPost("reject/{id}")]
    public async Task<IActionResult> RejectRequest(string id)
    {
        if (string.IsNullOrWhiteSpace(CurrentUsername)) return Unauthorized();

        var result = await connectionService.RejectRequestAsync(id, CurrentUsername);
        return Ok(result);
    }

    [HttpPost("dismiss/{id}")]
    public async Task<IActionResult> DismissRequest(string id)
    {
        if (string.IsNullOrWhiteSpace(CurrentUsername)) return Unauthorized();

        await connectionService.DismissRequestAsync(id, CurrentUsername);
        return Ok(new { message = "Notification dismissed" });
    }

    // ── Contacts ──

    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts([FromQuery] string? username)
    {
        if (string.IsNullOrWhiteSpace(CurrentUsername)) return Unauthorized();

        var targetUser = string.IsNullOrWhiteSpace(username) ? CurrentUsername : username.Trim().ToLower();
        if (targetUser != CurrentUsername)
            return Forbid();

        var result = await connectionService.GetContactsAsync(targetUser);
        return Ok(result);
    }

    // ── Messages ──

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages(
        [FromQuery] string user1, 
        [FromQuery] string user2,
        [FromQuery] int limit = 30,
        [FromQuery] string? beforeTimestamp = null)
    {
        if (string.IsNullOrWhiteSpace(CurrentUsername)) return Unauthorized();

        var u1 = user1?.Trim().ToLower() ?? "";
        var u2 = user2?.Trim().ToLower() ?? "";

        // Enforce that caller is an authorized participant of this conversation
        if (CurrentUsername != u1 && CurrentUsername != u2)
            return Forbid();

        var result = await connectionService.GetMessagesAsync(u1, u2, limit, beforeTimestamp);
        return Ok(result);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUsername)) return Unauthorized();

        // Prevent spoofing sender identity
        if (!string.Equals(CurrentUsername, dto.Sender?.Trim(), StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var result = await connectionService.SendMessageAsync(dto);
        return Ok(result);
    }

    [HttpPost("messages/read")]
    public async Task<IActionResult> MarkMessagesAsRead([FromBody] MarkReadDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUsername)) return Unauthorized();

        // Only reader can mark their unread messages as read
        if (!string.Equals(CurrentUsername, dto.User?.Trim(), StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var result = await connectionService.MarkMessagesAsReadAsync(dto);
        return Ok(result);
    }

    // ── Keys ──

    [HttpPost("keys")]
    public async Task<IActionResult> SavePublicKey([FromBody] PublicKeyDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUsername)) return Unauthorized();

        // Prevent overwriting someone else's public key (MitM protection)
        if (!string.Equals(CurrentUsername, dto.Username?.Trim(), StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var result = await connectionService.SavePublicKeyAsync(dto);
        return Ok(result);
    }

    [HttpGet("keys/{username}")]
    public async Task<IActionResult> GetPublicKey(string username)
    {
        // Any authenticated user can fetch recipient public keys to encrypt messages
        var result = await connectionService.GetPublicKeyAsync(username);
        return Ok(result);
    }

    // ── Presence ──

    [HttpPost("heartbeat")]
    public IActionResult Heartbeat([FromBody] HeartbeatDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUsername)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Username) || !string.Equals(CurrentUsername, dto.Username.Trim(), StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var result = connectionService.Heartbeat(dto.Username.Trim());
        return Ok(result);
    }

    [HttpPost("presence/offline")]
    public IActionResult SetOffline([FromBody] HeartbeatDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUsername)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Username) || !string.Equals(CurrentUsername, dto.Username.Trim(), StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var result = connectionService.SetOffline(dto.Username.Trim());
        return Ok(result);
    }
}
