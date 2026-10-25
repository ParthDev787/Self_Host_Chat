using API.DTOs;
using API.Interface;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/connections")]
public class ConnectionsController(IConnectionService connectionService) : ControllerBase
{
    // ── Connection Requests ──

    [HttpPost("request")]
    public async Task<IActionResult> SendRequest([FromBody] SendConnectionRequestDto dto)
    {
        var result = await connectionService.SendRequestAsync(dto);
        return Ok(result);
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests([FromQuery] string? username)
    {
        var result = await connectionService.GetRequestsAsync(username!);
        return Ok(result);
    }

    [HttpPost("accept/{id}")]
    public async Task<IActionResult> AcceptRequest(string id)
    {
        var result = await connectionService.AcceptRequestAsync(id);
        return Ok(result);
    }

    [HttpPost("reject/{id}")]
    public async Task<IActionResult> RejectRequest(string id)
    {
        var result = await connectionService.RejectRequestAsync(id);
        return Ok(result);
    }

    [HttpPost("dismiss/{id}")]
    public async Task<IActionResult> DismissRequest(string id)
    {
        await connectionService.DismissRequestAsync(id);
        return Ok(new { message = "Notification dismissed" });
    }

    // ── Contacts ──

    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts([FromQuery] string? username)
    {
        var result = await connectionService.GetContactsAsync(username!);
        return Ok(result);
    }

    // ── Messages ──

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages([FromQuery] string user1, [FromQuery] string user2)
    {
        var result = await connectionService.GetMessagesAsync(user1, user2);
        return Ok(result);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        var result = await connectionService.SendMessageAsync(dto);
        return Ok(result);
    }

    [HttpPost("messages/read")]
    public async Task<IActionResult> MarkMessagesAsRead([FromBody] MarkReadDto dto)
    {
        var result = await connectionService.MarkMessagesAsReadAsync(dto);
        return Ok(result);
    }

    // ── Keys ──

    [HttpPost("keys")]
    public async Task<IActionResult> SavePublicKey([FromBody] PublicKeyDto dto)
    {
        var result = await connectionService.SavePublicKeyAsync(dto);
        return Ok(result);
    }

    [HttpGet("keys/{username}")]
    public async Task<IActionResult> GetPublicKey(string username)
    {
        var result = await connectionService.GetPublicKeyAsync(username);
        return Ok(result);
    }

    // ── Presence ──

    [HttpPost("heartbeat")]
    public IActionResult Heartbeat([FromBody] HeartbeatDto dto)
    {
        var result = connectionService.Heartbeat(dto.Username);
        return Ok(result);
    }

    [HttpPost("presence/offline")]
    public IActionResult SetOffline([FromBody] HeartbeatDto dto)
    {
        var result = connectionService.SetOffline(dto.Username);
        return Ok(result);
    }
}
