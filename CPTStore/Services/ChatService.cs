using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace CPTStore.Services
{
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;

        public ChatService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> SaveMessageAsync(ChatMessage message)
        {
            // Đảm bảo thời gian gửi được thiết lập
            if (message.SentAt == default)
            {
                message.SentAt = DateTime.Now;
            }

            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();

            return message.Id;
        }

        public async Task<IEnumerable<ChatMessage>> GetChatHistoryAsync(string userId1, string userId2, int count = 20, int skip = 0)
        {
            return await _context.ChatMessages
                .Where(m => (m.SenderId == userId1 && m.ReceiverId == userId2) || 
                           (m.SenderId == userId2 && m.ReceiverId == userId1))
                .OrderByDescending(m => m.SentAt)
                .Skip(skip)
                .Take(count)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetRecentContactsAsync(string userId, int count = 10)
        {
            // Lấy danh sách người dùng có cuộc trò chuyện gần đây nhất với userId
            var recentContacts = await _context.ChatMessages
                .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                .OrderByDescending(m => m.SentAt)
                .Select(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
                .Distinct()
                .Take(count)
                .ToListAsync();

            return recentContacts;
        }

        public async Task<IEnumerable<ChatMessage>> GetUnreadMessagesAsync(string userId)
        {
            return await _context.ChatMessages
                .Where(m => m.ReceiverId == userId && !m.IsRead)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        public async Task MarkMessageAsReadAsync(int messageId)
        {
            var message = await _context.ChatMessages.FindAsync(messageId);
            if (message != null && !message.IsRead)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllMessagesAsReadAsync(string senderId, string receiverId)
        {
            var unreadMessages = await _context.ChatMessages
                .Where(m => m.SenderId == senderId && m.ReceiverId == receiverId && !m.IsRead)
                .ToListAsync();

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<int> GetUnreadMessageCountAsync(string userId)
        {
            return await _context.ChatMessages
                .CountAsync(m => m.ReceiverId == userId && !m.IsRead);
        }

        public Task<IEnumerable<string>> GetOnlineUsersAsync()
        {
            // Giả định: Thông tin người dùng online được lưu trong một bảng khác
            // Trong triển khai thực tế, bạn cần tạo một bảng UserOnlineStatus hoặc sử dụng SignalR để theo dõi
            // Đây chỉ là mẫu triển khai
            var onlineUsers = new List<string>();
            return Task.FromResult<IEnumerable<string>>(onlineUsers);
        }

        public Task<bool> IsUserOnlineAsync(string userId)
        {
            // Giả định: Kiểm tra trạng thái online của người dùng
            // Trong triển khai thực tế, bạn cần kiểm tra từ bảng UserOnlineStatus hoặc SignalR
            return Task.FromResult(false);
        }

        public Task SetUserOnlineAsync(string userId)
        {
            // Giả định: Đặt trạng thái người dùng là online
            // Trong triển khai thực tế, bạn cần cập nhật bảng UserOnlineStatus hoặc SignalR
            return Task.CompletedTask;
        }

        public Task SetUserOfflineAsync(string userId)
        {
            // Giả định: Đặt trạng thái người dùng là offline
            // Trong triển khai thực tế, bạn cần cập nhật bảng UserOnlineStatus hoặc SignalR
            return Task.CompletedTask;
        }

        // Các phương thức bổ sung không yêu cầu trong interface
        public async Task<bool> DeleteMessageAsync(int messageId, string userId)
        {
            var message = await _context.ChatMessages
                .FirstOrDefaultAsync(m => m.Id == messageId && 
                                         (m.SenderId == userId || m.ReceiverId == userId));

            if (message == null)
            {
                return false;
            }

            _context.ChatMessages.Remove(message);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteConversationAsync(string userId1, string userId2)
        {
            var messages = await _context.ChatMessages
                .Where(m => (m.SenderId == userId1 && m.ReceiverId == userId2) || 
                           (m.SenderId == userId2 && m.ReceiverId == userId1))
                .ToListAsync();

            if (!messages.Any())
            {
                return false;
            }

            _context.ChatMessages.RemoveRange(messages);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<ChatMessage?> GetMessageByIdAsync(int messageId)
        {
            return await _context.ChatMessages.FindAsync(messageId);
        }

        // Phương thức bổ sung cho admin
        public async Task<IEnumerable<ChatMessage>> GetAdminUnreadMessagesAsync(string adminId)
        {
            // Lấy tin nhắn chưa đọc gửi đến admin
            return await _context.ChatMessages
                .Where(m => m.ReceiverId == adminId && !m.IsRead)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<ChatSummary>> GetAdminChatSummariesAsync(string adminId)
        {
            // Lấy tóm tắt cuộc trò chuyện cho admin
            var userIds = await _context.ChatMessages
                .Where(m => m.SenderId == adminId || m.ReceiverId == adminId)
                .Select(m => m.SenderId == adminId ? m.ReceiverId : m.SenderId)
                .Distinct()
                .ToListAsync();

            var summaries = new List<ChatSummary>();

            foreach (var userId in userIds)
            {
                var lastMessage = await _context.ChatMessages
                    .Where(m => (m.SenderId == adminId && m.ReceiverId == userId) || 
                               (m.SenderId == userId && m.ReceiverId == adminId))
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                var unreadCount = await _context.ChatMessages
                    .CountAsync(m => m.SenderId == userId && m.ReceiverId == adminId && !m.IsRead);

                var user = await _context.Users.FindAsync(userId);

                summaries.Add(new ChatSummary
                {
                    UserId = userId,
                    UserName = user?.UserName ?? "Unknown",
                    LastMessage = lastMessage?.Content ?? "",
                    LastMessageTime = lastMessage?.SentAt ?? DateTime.MinValue,
                    UnreadCount = unreadCount
                });
            }

            return summaries.OrderByDescending(s => s.UnreadCount)
                .ThenByDescending(s => s.LastMessageTime);
        }
    // Lớp hỗ trợ để trả về tóm tắt cuộc trò chuyện
    public class ChatSummary
    {
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string LastMessage { get; set; } = "";
        public DateTime LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
    }
}
}