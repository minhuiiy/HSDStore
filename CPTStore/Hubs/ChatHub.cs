using Microsoft.AspNetCore.SignalR;
using CPTStore.Models;
using CPTStore.Services;
using Microsoft.AspNetCore.Authorization;

namespace CPTStore.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;

        public ChatHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        public async Task SendMessage(string receiverId, string message)
        {
            var senderId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(senderId))
            {
                throw new HubException("User not authenticated");
            }

            var chatMessage = new ChatMessage
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = message,
                MessageType = MessageType.Text,
                SentAt = DateTime.Now
            };

            // Lưu tin nhắn vào cơ sở dữ liệu
            await _chatService.SaveMessageAsync(chatMessage);

            // Gửi tin nhắn đến người nhận nếu họ đang online
            if (await _chatService.IsUserOnlineAsync(receiverId))
            {
                await Clients.User(receiverId).SendAsync("ReceiveMessage", senderId, message, DateTime.Now);
            }

            // Gửi xác nhận lại cho người gửi
            await Clients.Caller.SendAsync("MessageSent", receiverId, message, DateTime.Now);
        }

        public async Task MarkAsRead(string senderId, int messageId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId))
            {
                throw new HubException("User not authenticated");
            }

            await _chatService.MarkMessageAsReadAsync(messageId);
            await Clients.User(senderId).SendAsync("MessageRead", messageId);
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                await _chatService.SetUserOnlineAsync(userId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                await _chatService.SetUserOfflineAsync(userId);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}