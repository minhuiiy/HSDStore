using CPTStore.Models;

namespace CPTStore.Services
{
    public interface IChatService
    {
        Task<IEnumerable<ChatMessage>> GetChatHistoryAsync(string userId1, string userId2, int count = 20, int skip = 0);
        Task<IEnumerable<ChatMessage>> GetUnreadMessagesAsync(string userId);
        Task<int> GetUnreadMessageCountAsync(string userId);
        Task<int> SaveMessageAsync(ChatMessage message);
        Task MarkMessageAsReadAsync(int messageId);
        Task MarkAllMessagesAsReadAsync(string senderId, string receiverId);
        Task<bool> IsUserOnlineAsync(string userId);
        Task SetUserOnlineAsync(string userId);
        Task SetUserOfflineAsync(string userId);
        Task<IEnumerable<string>> GetOnlineUsersAsync();
        Task<IEnumerable<string>> GetRecentContactsAsync(string userId, int count = 10);
    }
}