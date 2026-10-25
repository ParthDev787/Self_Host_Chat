using API.Data;
using API.Entities;
using API.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Repositories;

public class MessageRepository(DataContext context) : IMessageRepository
{
    public async Task<List<ChatMessageEntity>> GetConversationAsync(string user1, string user2)
    {
        return await context.ChatMessages
            .Where(m => (m.Sender == user1 && m.Recipient == user2)
                     || (m.Sender == user2 && m.Recipient == user1))
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }

    public async Task<ChatMessageEntity?> GetLastMessageAsync(string user1, string user2)
    {
        return await context.ChatMessages
            .Where(m => (m.Sender == user1 && m.Recipient == user2)
                     || (m.Sender == user2 && m.Recipient == user1))
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetUnreadCountAsync(string sender, string recipient)
    {
        return await context.ChatMessages
            .CountAsync(m => m.Sender == sender && m.Recipient == recipient && !m.IsRead);
    }

    public async Task<List<ChatMessageEntity>> GetUnreadMessagesAsync(string sender, string recipient)
    {
        return await context.ChatMessages
            .Where(m => m.Sender == sender && m.Recipient == recipient && !m.IsRead)
            .ToListAsync();
    }

    public async Task AddAsync(ChatMessageEntity message)
    {
        context.ChatMessages.Add(message);
        await context.SaveChangesAsync();
    }

    public async Task<int> MarkAsReadAsync(string sender, string recipient)
    {
        var unread = await context.ChatMessages
            .Where(m => m.Sender == sender && m.Recipient == recipient && !m.IsRead)
            .ToListAsync();

        if (unread.Count == 0) return 0;

        var readAt = DateTime.UtcNow.ToString("o");
        foreach (var msg in unread)
        {
            msg.IsRead = true;
            msg.IsDelivered = true;
            msg.ReadAt = readAt;
        }

        await context.SaveChangesAsync();
        return unread.Count;
    }
}
