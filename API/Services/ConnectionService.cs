using System.Collections.Concurrent;
using System.Text.RegularExpressions;
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
    //  UNIFIED FAST SYNC (Batch API call)
    // ══════════════════════════════════════════════

    public async Task<object> GetSyncDataAsync(string username, string? activeChatWith, int limit = 30)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new BadRequestException("Username is required");

        var user = username.Trim().ToLower();

        // 1. Update presence heartbeat in memory
        _presenceMap[user] = DateTime.UtcNow;

        // 2. Fetch pending and rejected requests
        var requests = await connectionRepo.GetPendingAndRejectedRequestsAsync(user);

        // 3. Fetch contacts with latest message and unread counts
        var contacts = await GetContactsAsync(user);

        // 4. If an active conversation is open, fetch the latest 'limit' messages (default 30)
        List<ChatMessageEntity>? activeMessages = null;
        if (!string.IsNullOrWhiteSpace(activeChatWith))
        {
            var other = activeChatWith.Trim().ToLower();
            activeMessages = await messageRepo.GetConversationAsync(user, other, limit);
        }

        return new
        {
            requests,
            contacts,
            activeMessages,
            serverTime = DateTime.UtcNow.ToString("o")
        };
    }

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
            var match = Regex.Match(rawTo, @"[?&]join=([^&#/?\s]+)", RegexOptions.IgnoreCase);
            if (match.Success) rawTo = match.Groups[1].Value;
        }
        else if (rawTo.Contains("/connect/"))
        {
            var match = Regex.Match(rawTo, @"/connect/([^&#/?\s]+)", RegexOptions.IgnoreCase);
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

    public async Task<object> AcceptRequestAsync(string id, string? currentUsername = null)
    {
        var req = await connectionRepo.GetRequestByIdAsync(id)
            ?? throw new NotFoundException("Connection request not found");

        if (!string.IsNullOrWhiteSpace(currentUsername) &&
            !string.Equals(req.ToUsername, currentUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("You can only accept connection requests sent to you");
        }

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

    public async Task<object> RejectRequestAsync(string id, string? currentUsername = null)
    {
        var req = await connectionRepo.GetRequestByIdAsync(id)
            ?? throw new NotFoundException("Connection request not found");

        if (!string.IsNullOrWhiteSpace(currentUsername) &&
            !string.Equals(req.ToUsername, currentUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("You can only reject connection requests sent to you");
        }

        req.Status = "rejected";
        req.Timestamp = DateTime.UtcNow.ToString("o");
        await connectionRepo.UpdateRequestAsync(req);

        return new { message = "Connection rejected", request = req };
    }

    public async Task DismissRequestAsync(string id, string? currentUsername = null)
    {
        var req = await connectionRepo.GetRequestByIdAsync(id);
        if (req != null)
        {
            if (!string.IsNullOrWhiteSpace(currentUsername) &&
                !string.Equals(req.FromUsername, currentUsername, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(req.ToUsername, currentUsername, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("You can only dismiss your own connection notifications");
            }

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
                && (DateTime.UtcNow - lastSeenUtc) < TimeSpan.FromSeconds(15);

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

    public async Task<List<ChatMessageEntity>> GetMessagesAsync(
        string user1,
        string user2,
        int limit = 30,
        string? beforeTimestamp = null)
    {
        if (string.IsNullOrWhiteSpace(user1) || string.IsNullOrWhiteSpace(user2))
            throw new BadRequestException("Both user1 and user2 are required");

        return await messageRepo.GetConversationAsync(user1.Trim().ToLower(), user2.Trim().ToLower(), limit, beforeTimestamp);
    }

    public async Task<ChatMessageEntity> SendMessageAsync(SendMessageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Sender) || string.IsNullOrWhiteSpace(dto.Recipient))
            throw new BadRequestException("Sender and Recipient are required");

        var sender = dto.Sender.Trim().ToLower();
        var recipient = dto.Recipient.Trim().ToLower();

        if (!await connectionRepo.AreConnectedAsync(sender, recipient))
            throw new ForbiddenException("You can only send messages to accepted connections");

        var isEncrypted = !string.IsNullOrWhiteSpace(dto.Ciphertext) && !string.IsNullOrWhiteSpace(dto.Iv);
        var content = !string.IsNullOrWhiteSpace(dto.Text) ? dto.Text : (dto.Ciphertext ?? "");
        if (string.IsNullOrWhiteSpace(content))
            throw new BadRequestException("Message text or ciphertext is required");

        var msg = new ChatMessageEntity
        {
            Sender = sender,
            Recipient = recipient,
            Text = content.Trim(),
            Ciphertext = isEncrypted ? dto.Ciphertext!.Trim() : null,
            Iv = isEncrypted ? dto.Iv?.Trim() : null,
            Mac = isEncrypted ? dto.Mac?.Trim() : null,
            EphemeralPublicKey = isEncrypted ? dto.EphemeralPublicKey?.Trim() : null,
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
        if (diff.TotalSeconds < 15)
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
