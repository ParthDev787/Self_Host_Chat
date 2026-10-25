using API.Entities;

namespace API.Interface;

public interface IMessageRepository
{
    Task<List<ChatMessageEntity>> GetConversationAsync(string user1, string user2);
    Task<ChatMessageEntity?> GetLastMessageAsync(string user1, string user2);
    Task<int> GetUnreadCountAsync(string sender, string recipient);
    Task<List<ChatMessageEntity>> GetUnreadMessagesAsync(string sender, string recipient);
    Task AddAsync(ChatMessageEntity message);
    Task<int> MarkAsReadAsync(string sender, string recipient);
}
