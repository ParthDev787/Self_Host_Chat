using API.Data;
using API.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/connections")]
public class ConnectionsController(DataContext context) : ControllerBase
{
    public record RequestDto(string FromUsername, string ToUsername, string? Message);
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
    public record PublicKeyDto(string Username, string PublicKeyJwk, string? PreKeyJwk);
    public record MarkReadDto(string User, string WithUser);

    [HttpPost("request")]
    public async Task<IActionResult> SendRequest([FromBody] RequestDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.FromUsername) || string.IsNullOrWhiteSpace(dto.ToUsername))
                return BadRequest(new { error = "Both fromUsername and toUsername are required" });

            var from = dto.FromUsername.Trim().ToLower();
            var rawTo = dto.ToUsername.Trim();

            // Extract username if a full link was passed (e.g. http://localhost:4200/?join=admin)
            if (rawTo.Contains("join="))
            {
                var match = System.Text.RegularExpressions.Regex.Match(rawTo, @"[?&]join=([^&#/?\s]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success) rawTo = match.Groups[1].Value;
            }
            else if (rawTo.Contains("/connect/"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(rawTo, @"/connect/([^&#/?\s]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success) rawTo = match.Groups[1].Value;
            }

            var to = rawTo.Trim().ToLower();

            if (from == to)
                return BadRequest(new { error = "You cannot connect with yourself" });

            // Check if already connected
            var alreadyConnected = await context.UserConnections.AnyAsync(c =>
                (c.User1 == from && c.User2 == to) || (c.User1 == to && c.User2 == from));
            if (alreadyConnected)
                return BadRequest(new { error = $"Already connected with {to}" });

            // Check if request already exists
            var existingReq = await context.ConnectionRequests.FirstOrDefaultAsync(r =>
                r.FromUsername == from && r.ToUsername == to);

            if (existingReq != null)
            {
                // Re-activate or update existing request
                existingReq.Message = dto.Message;
                existingReq.Status = "pending";
                existingReq.Timestamp = DateTime.UtcNow.ToString("o");
                await context.SaveChangesAsync();
                return Ok(existingReq);
            }

            var newReq = new ConnectionRequest
            {
                FromUsername = from,
                ToUsername = to,
                Message = dto.Message,
                Status = "pending",
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            context.ConnectionRequests.Add(newReq);
            await context.SaveChangesAsync();

            return Ok(newReq);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests([FromQuery] string? username)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest(new { error = "Username is required" });

            var target = username.Trim().ToLower();

            // Return incoming requests addressed to target, 
            // plus outgoing requests sent by target that have been rejected so the sender gets notified
            var requests = await context.ConnectionRequests
                .Where(r => r.ToUsername == target || (r.FromUsername == target && r.Status == "rejected"))
                .OrderByDescending(r => r.Timestamp)
                .ToListAsync();

            return Ok(requests);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpPost("accept/{id}")]
    public async Task<IActionResult> AcceptRequest(string id)
    {
        try
        {
            var req = await context.ConnectionRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (req == null)
                return NotFound(new { error = "Connection request not found" });

            req.Status = "accepted";

            var alreadyConnected = await context.UserConnections.AnyAsync(c =>
                (c.User1 == req.FromUsername && c.User2 == req.ToUsername) ||
                (c.User1 == req.ToUsername && c.User2 == req.FromUsername));

            if (!alreadyConnected)
            {
                var conn = new UserConnection
                {
                    User1 = req.ToUsername,
                    User2 = req.FromUsername,
                    ConnectedAt = DateTime.UtcNow.ToString("o")
                };
                context.UserConnections.Add(conn);
            }

            if (!string.IsNullOrWhiteSpace(req.Message))
            {
                var msg = new ChatMessageEntity
                {
                    Sender = req.FromUsername,
                    Recipient = req.ToUsername,
                    Text = req.Message,
                    Timestamp = DateTime.UtcNow.ToString("o")
                };
                context.ChatMessages.Add(msg);
            }

            await context.SaveChangesAsync();
            return Ok(new { message = "Connection accepted", request = req });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpPost("reject/{id}")]
    public async Task<IActionResult> RejectRequest(string id)
    {
        try
        {
            var req = await context.ConnectionRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (req == null)
                return NotFound(new { error = "Connection request not found" });

            req.Status = "rejected";
            req.Timestamp = DateTime.UtcNow.ToString("o");
            await context.SaveChangesAsync();
            return Ok(new { message = "Connection rejected", request = req });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpPost("dismiss/{id}")]
    public async Task<IActionResult> DismissRequest(string id)
    {
        try
        {
            var req = await context.ConnectionRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (req != null)
            {
                context.ConnectionRequests.Remove(req);
                await context.SaveChangesAsync();
            }
            return Ok(new { message = "Notification dismissed" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts([FromQuery] string? username)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest(new { error = "Username is required" });

            var user = username.Trim().ToLower();

            var conns = await context.UserConnections
                .Where(c => c.User1 == user || c.User2 == user)
                .ToListAsync();

            var contactNames = conns
                .Select(c => c.User1 == user ? c.User2 : c.User1)
                .Distinct()
                .ToList();

            var contactList = new List<object>();

            foreach (var name in contactNames)
            {
                var lastMsg = await context.ChatMessages
                    .Where(m => (m.Sender == user && m.Recipient == name) || (m.Sender == name && m.Recipient == user))
                    .OrderByDescending(m => m.Timestamp)
                    .FirstOrDefaultAsync();

                // Unread messages sent by this contact to current user
                var unreadCount = await context.ChatMessages
                    .CountAsync(m => m.Sender == name && m.Recipient == user && !m.IsRead);

                string msgTime = "now";
                if (lastMsg != null && DateTime.TryParse(lastMsg.Timestamp, out var dt))
                {
                    msgTime = dt.ToLocalTime().ToString("t");
                }

                contactList.Add(new
                {
                    username = name,
                    status = "online",
                    lastMessage = lastMsg?.Text ?? "Connected! Say hi 👋",
                    lastMessageObj = lastMsg,
                    lastMessageTime = msgTime,
                    unreadCount = unreadCount,
                    lastMessageIsMine = lastMsg?.Sender == user,
                    lastMessageIsRead = lastMsg?.IsRead ?? false,
                    lastMessageIsDelivered = lastMsg?.IsDelivered ?? false
                });
            }

            return Ok(contactList);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages([FromQuery] string user1, [FromQuery] string user2)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(user1) || string.IsNullOrWhiteSpace(user2))
                return BadRequest(new { error = "Both user1 and user2 are required" });

            var u1 = user1.Trim().ToLower();
            var u2 = user2.Trim().ToLower();

            var messages = await context.ChatMessages
                .Where(m => (m.Sender == u1 && m.Recipient == u2) || (m.Sender == u2 && m.Recipient == u1))
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            return Ok(messages);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpPost("messages/read")]
    public async Task<IActionResult> MarkMessagesAsRead([FromBody] MarkReadDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.User) || string.IsNullOrWhiteSpace(dto.WithUser))
                return BadRequest(new { error = "Both user and withUser are required" });

            var reader = dto.User.Trim().ToLower();
            var sender = dto.WithUser.Trim().ToLower();

            var unread = await context.ChatMessages
                .Where(m => m.Sender == sender && m.Recipient == reader && !m.IsRead)
                .ToListAsync();

            if (unread.Count > 0)
            {
                var readAt = DateTime.UtcNow.ToString("o");
                foreach (var msg in unread)
                {
                    msg.IsRead = true;
                    msg.IsDelivered = true;
                    msg.ReadAt = readAt;
                }
                await context.SaveChangesAsync();
            }

            return Ok(new { success = true, markedCount = unread.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Sender) || string.IsNullOrWhiteSpace(dto.Recipient))
                return BadRequest(new { error = "Sender and Recipient are required" });

            var content = dto.Ciphertext ?? dto.Text;
            if (string.IsNullOrWhiteSpace(content))
                return BadRequest(new { error = "Ciphertext or Text is required" });

            var msg = new ChatMessageEntity
            {
                Sender = dto.Sender.Trim().ToLower(),
                Recipient = dto.Recipient.Trim().ToLower(),
                Text = content.Trim(),
                Ciphertext = dto.Ciphertext?.Trim() ?? content.Trim(),
                Iv = dto.Iv?.Trim(),
                Mac = dto.Mac?.Trim(),
                EphemeralPublicKey = dto.EphemeralPublicKey?.Trim(),
                Sequence = dto.Sequence,
                IsDelivered = true,
                IsRead = false,
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            context.ChatMessages.Add(msg);
            await context.SaveChangesAsync();

            return Ok(msg);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpPost("keys")]
    public async Task<IActionResult> SavePublicKey([FromBody] PublicKeyDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.PublicKeyJwk))
                return BadRequest(new { error = "Username and PublicKeyJwk are required" });

            var uname = dto.Username.Trim().ToLower();
            var keyRecord = await context.UserPublicKeys.FirstOrDefaultAsync(k => k.Username == uname);
            if (keyRecord == null)
            {
                keyRecord = new UserPublicKey
                {
                    Username = uname,
                    PublicKeyJwk = dto.PublicKeyJwk.Trim(),
                    PreKeyJwk = dto.PreKeyJwk?.Trim(),
                    UpdatedAt = DateTime.UtcNow.ToString("o")
                };
                context.UserPublicKeys.Add(keyRecord);
            }
            else
            {
                keyRecord.PublicKeyJwk = dto.PublicKeyJwk.Trim();
                keyRecord.PreKeyJwk = dto.PreKeyJwk?.Trim();
                keyRecord.UpdatedAt = DateTime.UtcNow.ToString("o");
            }

            await context.SaveChangesAsync();
            return Ok(new { message = "Public key registered successfully", key = keyRecord });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("keys/{username}")]
    public async Task<IActionResult> GetPublicKey(string username)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest(new { error = "Username is required" });

            var uname = username.Trim().ToLower();
            var keyRecord = await context.UserPublicKeys.FirstOrDefaultAsync(k => k.Username == uname);
            if (keyRecord == null)
                return NotFound(new { error = $"Public key not found for {username}" });

            return Ok(keyRecord);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
