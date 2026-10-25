using API.DTOs;
using API.Entities;

namespace API.Interface;

public interface IConnectionService
{
    // ── Connection Requests ──
    Task<ConnectionRequest> SendRequestAsync(SendConnectionRequestDto dto);
    Task<List<ConnectionRequest>> GetRequestsAsync(string username);
    Task<object> AcceptRequestAsync(string id);
    Task<object> RejectRequestAsync(string id);
    Task DismissRequestAsync(string id);

    // ── Contacts ──
    Task<List<object>> GetContactsAsync(string username);

    // ── Messages ──
    Task<List<ChatMessageEntity>> GetMessagesAsync(string user1, string user2);
    Task<ChatMessageEntity> SendMessageAsync(SendMessageDto dto);
    Task<object> MarkMessagesAsReadAsync(MarkReadDto dto);

    // ── Keys ──
    Task<object> SavePublicKeyAsync(PublicKeyDto dto);
    Task<UserPublicKey> GetPublicKeyAsync(string username);

    // ── Presence ──
    object Heartbeat(string username);
    object SetOffline(string username);
}
