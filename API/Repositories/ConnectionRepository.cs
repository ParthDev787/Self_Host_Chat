using API.Data;
using API.Entities;
using API.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Repositories;

public class ConnectionRepository(DataContext context) : IConnectionRepository
{
    // ── Connection Requests ──

    public async Task<ConnectionRequest?> GetRequestByIdAsync(string id)
    {
        return await context.ConnectionRequests.FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<ConnectionRequest?> GetRequestAsync(string fromUsername, string toUsername)
    {
        return await context.ConnectionRequests
            .FirstOrDefaultAsync(r => r.FromUsername == fromUsername && r.ToUsername == toUsername);
    }

    public async Task<List<ConnectionRequest>> GetPendingAndRejectedRequestsAsync(string username)
    {
        return await context.ConnectionRequests
            .Where(r => (r.ToUsername == username && r.Status == "pending")
                     || (r.FromUsername == username && r.Status == "rejected"))
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync();
    }

    public async Task AddRequestAsync(ConnectionRequest request)
    {
        context.ConnectionRequests.Add(request);
        await context.SaveChangesAsync();
    }

    public async Task UpdateRequestAsync(ConnectionRequest request)
    {
        context.ConnectionRequests.Update(request);
        await context.SaveChangesAsync();
    }

    public async Task RemoveRequestAsync(ConnectionRequest request)
    {
        context.ConnectionRequests.Remove(request);
        await context.SaveChangesAsync();
    }

    public async Task AcceptRequestAsync(ConnectionRequest request, UserConnection? connection, ChatMessageEntity? initialMessage)
    {
        if (connection != null)
        {
            context.UserConnections.Add(connection);
        }
        if (initialMessage != null)
        {
            context.ChatMessages.Add(initialMessage);
        }
        context.ConnectionRequests.Remove(request);
        await context.SaveChangesAsync();
    }

    // ── User Connections ──

    public async Task<bool> AreConnectedAsync(string user1, string user2)
    {
        return await context.UserConnections.AnyAsync(c =>
            (c.User1 == user1 && c.User2 == user2) || (c.User1 == user2 && c.User2 == user1));
    }

    public async Task<List<UserConnection>> GetConnectionsForUserAsync(string username)
    {
        return await context.UserConnections
            .Where(c => c.User1 == username || c.User2 == username)
            .ToListAsync();
    }

    public async Task AddConnectionAsync(UserConnection connection)
    {
        context.UserConnections.Add(connection);
        await context.SaveChangesAsync();
    }
}
