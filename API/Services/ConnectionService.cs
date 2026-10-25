using System.Collections.Concurrent;
using API.DTOs;
using API.Entities;
using API.Errors;
using API.Interface;

namespace API.Services;

public class ConnectionService(
    IConnectionRepository connectionRepo,
    IMessageRepository messageRepo,
    IKeyRepository keyRepo) : IConnectionService
{
    private static readonly ConcurrentDictionary<string, DateTime> _presenceMap = new(StringComparer.OrdinalIgnoreCase);

    // ══════════════════════════════════════════════
    //  CONNECTION REQUESTS
    // ══════════════════════════════════════════════

    public async Task<ConnectionRequest> SendRequestAsync(SendConnectionRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FromUsername) || string.IsNullOrWhiteSpace(dto.ToUsername))
            throw new BadRequestException("Both fromUsername and toUsername are required");

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
            throw new BadRequestException("You cannot connect with yourself");

        if (await connectionRepo.AreConnectedAsync(from, to))
            throw new BadRequestException($"Already connected with {to}");

        // Check if request already exists — reactivate it
        var existingReq = await connectionRepo.GetRequestAsync(from, to);
        if (existingReq != null)
        {
            existingReq.Message = dto.Message;
            existingReq.Status = "pending";
            existingReq.Timestamp = DateTime.UtcNow.ToString("o");
            await connectionRepo.UpdateRequestAsync(existingReq);
            return existingReq;
        }

        var newReq = new ConnectionRequest
        {
            FromUsername = from,
            ToUsername = to,
            Message = dto.Message,
            Status = "pending",
            Timestamp = DateTime.UtcNow.ToString("o")
        };

        await connectionRepo.AddRequestAsync(newReq);
        return newReq;
    }

    public async Task<List<ConnectionRequest>> GetRequestsAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new BadRequestException("Username is required");

        return await connectionRepo.GetPendingAndRejectedRequestsAsync(username.Trim().ToLower());
    }

    public async Task<object> AcceptRequestAsync(string id)
    {
        var req = await connectionRepo.GetRequestByIdAsync(id)
            ?? throw new NotFoundException("Connection request not found");

        req.Status = "accepted";

        UserConnection? newConnection = null;
        if (!await connectionRepo.AreConnectedAsync(req.FromUsername, req.ToUsername))
        {
            newConnection = new UserConnection
            {
                User1 = req.ToUsername,
                User2 = req.FromUsername,
                ConnectedAt = DateTime.UtcNow.ToString("o")
            };
        }

        ChatMessageEntity? initialMessage = null;
        if (!string.IsNullOrWhiteSpace(req.Message))
        {
            initialMessage = new ChatMessageEntity
            {
                Sender = req.FromUsername,
                Recipient = req.ToUsername,
                Text = req.Message,
                Timestamp = DateTime.UtcNow.ToString("o")
            };
        }

        await connectionRepo.AcceptRequestAsync(req, newConnection, initialMessage);

        return new { message = "Connection accepted", request = req };
    }

    public async Task<object> RejectRequestAsync(string id)
    {
        var req = await connectionRepo.GetRequestByIdAsync(id)
            ?? throw new NotFoundException("Connection request not found");

        req.Status = "rejected";
        req.Timestamp = DateTime.UtcNow.ToString("o");
        await connectionRepo.UpdateRequestAsync(req);

        return new { message = "Connection rejected", request = req };
    }

    public async Task DismissRequestAsync(string id)
    {
        var req = await connectionRepo.GetRequestByIdAsync(id);
        if (req != null)
        {
            await connectionRepo.RemoveRequestAsync(req);
        }
    }

    // ══════════════════════════════════════════════
    //  CONTACTS
    // ══════════════════════════════════════════════

    public async Task<List<object>> GetContactsAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new BadRequestException("Username is required");

        var user = username.Trim().ToLower();
        _presenceMap[user] = DateTime.UtcNow;

        var conns = await connectionRepo.GetConnectionsForUserAsync(user);

        var contactNames = conns
            .Select(c => c.User1 == user ? c.User2 : c.User1)
            .Distinct()
            .ToList();

        var contactList = new List<object>();

        foreach (var name in contactNames)
        {
            var lastMsg = await messageRepo.GetLastMessageAsync(user, name);
            var unreadCount = await messageRepo.GetUnreadCountAsync(name, user);

            string msgTime = "now";
            if (lastMsg != null && DateTime.TryParse(lastMsg.Timestamp, out var dt))
            {
                msgTime = dt.ToLocalTime().ToString("t");
            }

            bool isOnline = _presenceMap.TryGetValue(name, out var lastSeenUtc)
                && (DateTime.UtcNow - lastSeenUtc) < TimeSpan.FromSeconds(5);

            contactList.Add(new
            {
                username = name,
                status = isOnline ? "online" : "offline",
                lastSeen = isOnline ? "Online" : FormatLastSeen(lastSeenUtc),
                lastSeenTime = lastSeenUtc != DateTime.MinValue ? lastSeenUtc.ToString("o") : null,
                lastMessage = lastMsg?.Text ?? "Connected! Say hi 👋",
                lastMessageObj = lastMsg,
                lastMessageTime = msgTime,
                unreadCount,
                lastMessageIsMine = lastMsg?.Sender == user,
                lastMessageIsRead = lastMsg?.IsRead ?? false,
                lastMessageIsDelivered = lastMsg?.IsDelivered ?? false
            });
        }

        return contactList;
    }

    // ══════════════════════════════════════════════
    //  MESSAGES
    // ══════════════════════════════════════════════

    public async Task<List<ChatMessageEntity>> GetMessagesAsync(string user1, string user2)
    {
        if (string.IsNullOrWhiteSpace(user1) || string.IsNullOrWhiteSpace(user2))
            throw new BadRequestException("Both user1 and user2 are required");

        return await messageRepo.GetConversationAsync(user1.Trim().ToLower(), user2.Trim().ToLower());
    }

    public async Task<ChatMessageEntity> SendMessageAsync(SendMessageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Sender) || string.IsNullOrWhiteSpace(dto.Recipient))
            throw new BadRequestException("Sender and Recipient are required");

        var content = dto.Ciphertext ?? dto.Text;
        if (string.IsNullOrWhiteSpace(content))
            throw new BadRequestException("Ciphertext or Text is required");

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

        await messageRepo.AddAsync(msg);
        return msg;
    }

    public async Task<object> MarkMessagesAsReadAsync(MarkReadDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.User) || string.IsNullOrWhiteSpace(dto.WithUser))
            throw new BadRequestException("Both user and withUser are required");

        var reader = dto.User.Trim().ToLower();
        var sender = dto.WithUser.Trim().ToLower();

        var markedCount = await messageRepo.MarkAsReadAsync(sender, reader);

        return new { success = true, markedCount };
    }

    // ══════════════════════════════════════════════
    //  KEYS
    // ══════════════════════════════════════════════

    public async Task<object> SavePublicKeyAsync(PublicKeyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.PublicKeyJwk))
            throw new BadRequestException("Username and PublicKeyJwk are required");

        var uname = dto.Username.Trim().ToLower();
        var keyRecord = new UserPublicKey
        {
            Username = uname,
            PublicKeyJwk = dto.PublicKeyJwk.Trim(),
            PreKeyJwk = dto.PreKeyJwk?.Trim(),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };

        await keyRepo.SaveKeyAsync(keyRecord);
        return new { message = "Public key registered successfully", key = keyRecord };
    }

    public async Task<UserPublicKey> GetPublicKeyAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new BadRequestException("Username is required");

        return await keyRepo.GetByUsernameAsync(username.Trim().ToLower())
            ?? throw new NotFoundException($"Public key not found for {username}");
    }

    // ══════════════════════════════════════════════
    //  PRESENCE
    // ══════════════════════════════════════════════

    public object Heartbeat(string username)
    {
        if (!string.IsNullOrWhiteSpace(username))
            _presenceMap[username.Trim().ToLower()] = DateTime.UtcNow;

        return new { success = true, isOnline = true, timestamp = DateTime.UtcNow };
    }

    public object SetOffline(string username)
    {
        if (!string.IsNullOrWhiteSpace(username))
            _presenceMap.TryRemove(username.Trim().ToLower(), out _);

        return new { success = true, isOnline = false };
    }

    // ══════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════

    private static string FormatLastSeen(DateTime lastSeenUtc)
    {
        if (lastSeenUtc == DateTime.MinValue)
            return "Offline";

        var diff = DateTime.UtcNow - lastSeenUtc;
        if (diff.TotalSeconds < 7)
            return "Online";
        if (diff.TotalMinutes < 1)
            return "Last seen just now";
        if (diff.TotalMinutes < 60)
            return $"Last seen {(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24)
            return $"Last seen at {lastSeenUtc.ToLocalTime():t}";
        if (diff.TotalDays < 2)
            return $"Last seen yesterday at {lastSeenUtc.ToLocalTime():t}";
        return $"Last seen on {lastSeenUtc.ToLocalTime():MMM d}";
    }
}
