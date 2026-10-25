using API.Entities;

namespace API.Interface;

public interface IConnectionRepository
{
    // ── Connection Requests ──
    Task<ConnectionRequest?> GetRequestByIdAsync(string id);
    Task<ConnectionRequest?> GetRequestAsync(string fromUsername, string toUsername);
    Task<List<ConnectionRequest>> GetPendingAndRejectedRequestsAsync(string username);
    Task AddRequestAsync(ConnectionRequest request);
    Task UpdateRequestAsync(ConnectionRequest request);
    Task RemoveRequestAsync(ConnectionRequest request);
    Task AcceptRequestAsync(ConnectionRequest request, UserConnection? connection, ChatMessageEntity? initialMessage);

    // ── User Connections ──
    Task<bool> AreConnectedAsync(string user1, string user2);
    Task<List<UserConnection>> GetConnectionsForUserAsync(string username);
    Task AddConnectionAsync(UserConnection connection);
}
