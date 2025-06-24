namespace CPTStore.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
        Task SendEmailWithAttachmentAsync(string to, string subject, string body, byte[] attachment, string attachmentName, string attachmentContentType, bool isHtml = true);
        Task SendOrderConfirmationAsync(int orderId);
        Task SendOrderStatusUpdateAsync(int orderId);
        Task SendPasswordResetAsync(string email, string resetLink);
        Task SendWelcomeEmailAsync(string email, string userName);
        Task SendLowStockNotificationAsync(int productId);
    }
}