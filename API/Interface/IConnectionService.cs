using API.DTOs;
using API.Entities;

namespace API.Interface;

public interface IConnectionService
{
    // ── Unified Fast Sync (Reduces multiple API calls to 1) ──
    Task<object> GetSyncDataAsync(string username, string? activeChatWith, int limit = 30);

    // ── Connection Requests ──
    Task<ConnectionRequest> SendRequestAsync(SendConnectionRequestDto dto);
    Task<List<ConnectionRequest>> GetRequestsAsync(string username);
    Task<object> AcceptRequestAsync(string id, string? currentUsername = null);
    Task<object> RejectRequestAsync(string id, string? currentUsername = null);
    Task DismissRequestAsync(string id, string? currentUsername = null);

    // ── Contacts ──
    Task<List<object>> GetContactsAsync(string username);

    // ── Messages ──
    Task<List<ChatMessageEntity>> GetMessagesAsync(string user1, string user2, int limit = 30, string? beforeTimestamp = null);
    Task<ChatMessageEntity> SendMessageAsync(SendMessageDto dto);
    Task<object> MarkMessagesAsReadAsync(MarkReadDto dto);

    // ── Keys ──
    Task<object> SavePublicKeyAsync(PublicKeyDto dto);
    Task<UserPublicKey> GetPublicKeyAsync(string username);

    // ── Presence ──
    object Heartbeat(string username);
    object SetOffline(string username);
}
