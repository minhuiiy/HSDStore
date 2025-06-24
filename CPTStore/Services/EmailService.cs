using CPTStore.Data;
using CPTStore.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using System.IO;

namespace CPTStore.Services
{
    public class EmailService(ApplicationDbContext context, IConfiguration configuration) : IEmailService
    {
        private readonly ApplicationDbContext _context = context;
        private readonly string _smtpServer = configuration["EmailSettings:SmtpServer"] ?? "smtp.example.com";
        private readonly int _smtpPort = int.Parse(configuration["EmailSettings:SmtpPort"] ?? "587");
        private readonly string _smtpUsername = configuration["EmailSettings:SmtpUsername"] ?? "username";
        private readonly string _smtpPassword = configuration["EmailSettings:SmtpPassword"] ?? "password";
        private readonly string _senderEmail = configuration["EmailSettings:SenderEmail"] ?? "noreply@cptstore.com";
        private readonly string _senderName = configuration["EmailSettings:SenderName"] ?? "CPT Store";

        #region Email Sending Methods

        public async Task SendOrderConfirmationAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return;
            }

            var user = await _context.Users.FindAsync(order.UserId);
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                return;
            }

            string subject = $"Xác nhận đơn hàng #{order.OrderNumber}";
            string body = GenerateOrderConfirmationEmailBody(order);

            await SendEmailAsync(user.Email, subject, body, true);
        }

        public async Task SendOrderStatusUpdateAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return;
            }

            var user = await _context.Users.FindAsync(order.UserId);
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                return;
            }

            string subject = $"Cập nhật trạng thái đơn hàng #{order.OrderNumber}";
            string body = GenerateOrderStatusUpdateEmailBody(order);

            await SendEmailAsync(user.Email, subject, body, true);
        }

        public async Task SendPaymentConfirmationAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return;
            }

            var user = await _context.Users.FindAsync(order.UserId);
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                return;
            }

            string subject = $"Xác nhận thanh toán đơn hàng #{order.OrderNumber}";
            string body = GeneratePaymentConfirmationEmailBody(order);

            await SendEmailAsync(user.Email, subject, body, true);
        }

        public async Task SendRefundConfirmationAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return;
            }

            var user = await _context.Users.FindAsync(order.UserId);
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                return;
            }

            string subject = $"Xác nhận hoàn tiền đơn hàng #{order.OrderNumber}";
            string body = GenerateRefundConfirmationEmailBody(order);

            await SendEmailAsync(user.Email, subject, body, true);
        }

        public async Task SendLowStockNotificationAsync(int productId)
        {
            var inventory = await _context.Inventories
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.ProductId == productId);

            if (inventory == null)
            {
                return;
            }

            // Lấy danh sách email của admin
            var adminEmails = await _context.Users
                .Where(u => u.IsAdmin)
                .Select(u => u.Email)
                .ToListAsync();

            if (adminEmails.Count == 0)
            {
                return;
            }

            string subject = $"Thông báo tồn kho thấp: {inventory.Product?.Name ?? "Không xác định"}";
            string body = GenerateLowStockNotificationEmailBody(inventory);

            foreach (var email in adminEmails)
            {
                if (!string.IsNullOrEmpty(email))
                {
                    await SendEmailAsync(email, subject, body, true);
                }
            }
        }

        public async Task SendWelcomeEmailAsync(string email, string username)
        {
            string subject = "Chào mừng bạn đến với CPT Store";
            string body = GenerateWelcomeEmailBody(username);

            await SendEmailAsync(email, subject, body, true);
        }

        public async Task SendPasswordResetAsync(string email, string resetToken)
        {
            string subject = "Đặt lại mật khẩu CPT Store";
            string body = GeneratePasswordResetEmailBody(resetToken);

            await SendEmailAsync(email, subject, body, true);
        }

        public async Task SendContactFormConfirmationAsync(string email, string name, string message)
        {
            string subject = "Xác nhận liên hệ - CPT Store";
            string body = GenerateContactFormConfirmationEmailBody(name, message);

            await SendEmailAsync(email, subject, body, true);

            // Gửi thông báo cho admin
            var adminEmails = await _context.Users
                .Where(u => u.IsAdmin)
                .Select(u => u.Email)
                .ToListAsync();

            if (adminEmails.Count > 0)
            {
                string adminSubject = $"Tin nhắn liên hệ mới từ {name}";
                string adminBody = GenerateContactFormNotificationEmailBody(name, email, message);

                foreach (var adminEmail in adminEmails)
                {
                    if (!string.IsNullOrEmpty(adminEmail))
                    {
                        await SendEmailAsync(adminEmail, adminSubject, adminBody, true);
                    }
                }
            }
        }

        #region Interface Implementation

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            try
            {
                var message = new MailMessage
                {
                    From = new MailAddress(_senderEmail, _senderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };

                message.To.Add(new MailAddress(to));

                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);

                await client.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                // Log lỗi
                Console.WriteLine($"Error sending email: {ex.Message}");
            }
        }

        public async Task SendEmailWithAttachmentAsync(string to, string subject, string body, byte[] attachment, string attachmentName, string attachmentContentType, bool isHtml = true)
        {
            try
            {
                var message = new MailMessage
                {
                    From = new MailAddress(_senderEmail, _senderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };

                message.To.Add(new MailAddress(to));

                // Thêm tệp đính kèm
                using var ms = new MemoryStream(attachment);
                var attachmentItem = new Attachment(ms, attachmentName, attachmentContentType);
                message.Attachments.Add(attachmentItem);

                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);

                await client.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                // Log lỗi
                Console.WriteLine($"Error sending email with attachment: {ex.Message}");
            }
        }

        #endregion // Email Sending Methods

        #endregion // Interface Implementation

        #region Email Templates

        private static string GenerateOrderConfirmationEmailBody(Order order)
        {
            var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Xác nhận đơn hàng</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 10px; text-align: center; }}
        .footer {{ background-color: #f1f1f1; padding: 10px; text-align: center; font-size: 12px; }}
        table {{ width: 100%; border-collapse: collapse; margin-bottom: 20px; }}
        th, td {{ padding: 10px; text-align: left; border-bottom: 1px solid #ddd; }}
        th {{ background-color: #f2f2f2; }}
        .total {{ font-weight: bold; text-align: right; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Xác nhận đơn hàng</h1>
        </div>
        <p>Kính gửi Quý khách,</p>
        <p>Cảm ơn bạn đã đặt hàng tại CPT Store. Đơn hàng của bạn đã được xác nhận và đang được xử lý.</p>
        <h2>Thông tin đơn hàng #{order.OrderNumber}</h2>
        <p><strong>Ngày đặt hàng:</strong> {order.CreatedAt:dd/MM/yyyy HH:mm}</p>
        <p><strong>Phương thức thanh toán:</strong> {order.PaymentMethod}</p>
        <p><strong>Trạng thái thanh toán:</strong> {order.PaymentStatus}</p>
        <p><strong>Trạng thái đơn hàng:</strong> {order.Status}</p>
        <h3>Thông tin giao hàng</h3>
        <p><strong>Tên người nhận:</strong> {order.CustomerName}</p>
        <p><strong>Số điện thoại:</strong> {order.CustomerPhone}</p>
        <p><strong>Địa chỉ:</strong> {order.CustomerAddress}</p>
        <h3>Chi tiết đơn hàng</h3>
        <table>
            <tr>
                <th>Sản phẩm</th>
                <th>Số lượng</th>
                <th>Đơn giá</th>
                <th>Thành tiền</th>
            </tr>";

            foreach (var item in order.OrderItems)
            {
                body += $@"
            <tr>
                <td>{item.Product?.Name ?? "Không xác định"}</td>
                <td>{item.Quantity}</td>
                <td>{item.Price:N0} VNĐ</td>
                <td>{(item.Price * item.Quantity):N0} VNĐ</td>
            </tr>";
            }

            body += $@"
        </table>
        <p class='total'>Tổng tiền hàng: {order.TotalAmount:N0} VNĐ</p>";

            if (!string.IsNullOrEmpty(order.DiscountCode))
            {
                body += $@"
        <p class='total'>Giảm giá: {order.DiscountAmount:N0} VNĐ</p>
        <p class='total'>Thành tiền: {(order.TotalAmount - order.DiscountAmount):N0} VNĐ</p>";
            }

            body += $@"
        <p>Nếu bạn có bất kỳ câu hỏi nào về đơn hàng, vui lòng liên hệ với chúng tôi qua email support@cptstore.com hoặc số điện thoại 1900 1234.</p>
        <p>Trân trọng,</p>
        <p>Đội ngũ CPT Store</p>
        <div class='footer'>
            <p>© {DateTime.Now.Year} CPT Store. Tất cả các quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";

            return body;
        }

        private static string GenerateOrderStatusUpdateEmailBody(Order order)
        {
            var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Cập nhật trạng thái đơn hàng</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 10px; text-align: center; }}
        .footer {{ background-color: #f1f1f1; padding: 10px; text-align: center; font-size: 12px; }}
        .status {{ font-size: 18px; font-weight: bold; color: #4CAF50; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Cập nhật trạng thái đơn hàng</h1>
        </div>
        <p>Kính gửi Quý khách,</p>
        <p>Đơn hàng #{order.OrderNumber} của bạn đã được cập nhật trạng thái.</p>
        <p>Trạng thái đơn hàng hiện tại: <span class='status'>{order.Status}</span></p>";

            body += order.Status switch
            {
                OrderStatus.Processing => "<p>Đơn hàng của bạn đang được xử lý và chuẩn bị hàng.</p>",
                OrderStatus.Shipped => "<p>Đơn hàng của bạn đã được giao cho đơn vị vận chuyển và đang trên đường đến với bạn.</p>",
                OrderStatus.Delivered => "<p>Đơn hàng của bạn đã được giao thành công. Cảm ơn bạn đã mua sắm tại CPT Store!</p>",
                OrderStatus.Cancelled => "<p>Đơn hàng của bạn đã bị hủy. Nếu bạn có bất kỳ thắc mắc nào, vui lòng liên hệ với chúng tôi.</p>",
                OrderStatus.Refunded => "<p>Đơn hàng của bạn đã được hoàn tiền. Số tiền sẽ được hoàn trả vào phương thức thanh toán ban đầu của bạn trong vòng 5-7 ngày làm việc.</p>",
                _ => "<p>Vui lòng kiểm tra tài khoản của bạn để biết thêm chi tiết.</p>"
            };

            body += $@"
        <p>Để xem chi tiết đơn hàng, vui lòng đăng nhập vào tài khoản của bạn trên trang web của chúng tôi.</p>
        <p>Nếu bạn có bất kỳ câu hỏi nào, vui lòng liên hệ với chúng tôi qua email support@cptstore.com hoặc số điện thoại 1900 1234.</p>
        <p>Trân trọng,</p>
        <p>Đội ngũ CPT Store</p>
        <div class='footer'>
            <p>© {DateTime.Now.Year} CPT Store. Tất cả các quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";

            return body;
        }

        private static string GeneratePaymentConfirmationEmailBody(Order order)
        {
            var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Xác nhận thanh toán</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 10px; text-align: center; }}
        .footer {{ background-color: #f1f1f1; padding: 10px; text-align: center; font-size: 12px; }}
        .payment-info {{ background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin-bottom: 20px; }}
        .total {{ font-weight: bold; font-size: 18px; color: #4CAF50; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Xác nhận thanh toán</h1>
        </div>
        <p>Kính gửi Quý khách,</p>
        <p>Chúng tôi xin thông báo rằng thanh toán cho đơn hàng #{order.OrderNumber} của bạn đã được xác nhận thành công.</p>
        <div class='payment-info'>
            <p><strong>Mã đơn hàng:</strong> #{order.OrderNumber}</p>
            <p><strong>Ngày thanh toán:</strong> {order.UpdatedAt:dd/MM/yyyy HH:mm}</p>
            <p><strong>Phương thức thanh toán:</strong> {order.PaymentMethod}</p>";

            if (!string.IsNullOrEmpty(order.TransactionId))
            {
                body += $@"
            <p><strong>Mã giao dịch:</strong> {order.TransactionId}</p>";
            }

            body += $@"
            <p class='total'>Số tiền đã thanh toán: {order.TotalAmount:N0} VNĐ</p>
        </div>
        <p>Đơn hàng của bạn đang được xử lý và sẽ được giao đến bạn trong thời gian sớm nhất.</p>
        <p>Cảm ơn bạn đã mua sắm tại CPT Store!</p>
        <p>Trân trọng,</p>
        <p>Đội ngũ CPT Store</p>
        <div class='footer'>
            <p>© {DateTime.Now.Year} CPT Store. Tất cả các quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";

            return body;
        }

        private static string GenerateRefundConfirmationEmailBody(Order order)
        {
            var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Xác nhận hoàn tiền</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 10px; text-align: center; }}
        .footer {{ background-color: #f1f1f1; padding: 10px; text-align: center; font-size: 12px; }}
        .refund-info {{ background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin-bottom: 20px; }}
        .total {{ font-weight: bold; font-size: 18px; color: #4CAF50; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Xác nhận hoàn tiền</h1>
        </div>
        <p>Kính gửi Quý khách,</p>
        <p>Chúng tôi xin thông báo rằng yêu cầu hoàn tiền cho đơn hàng #{order.OrderNumber} của bạn đã được xử lý thành công.</p>
        <div class='refund-info'>
            <p><strong>Mã đơn hàng:</strong> #{order.OrderNumber}</p>
            <p><strong>Ngày hoàn tiền:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</p>
            <p><strong>Phương thức thanh toán gốc:</strong> {order.PaymentMethod}</p>
            <p class='total'>Số tiền hoàn trả: {order.TotalAmount:N0} VNĐ</p>
        </div>
        <p>Số tiền hoàn trả sẽ được chuyển về phương thức thanh toán ban đầu của bạn trong vòng 5-7 ngày làm việc, tùy thuộc vào chính sách của ngân hàng hoặc đơn vị thanh toán.</p>
        <p>Nếu bạn có bất kỳ câu hỏi nào về việc hoàn tiền, vui lòng liên hệ với chúng tôi qua email support@cptstore.com hoặc số điện thoại 1900 1234.</p>
        <p>Trân trọng,</p>
        <p>Đội ngũ CPT Store</p>
        <div class='footer'>
            <p>© {DateTime.Now.Year} CPT Store. Tất cả các quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";

            return body;
        }

        private static string GenerateLowStockNotificationEmailBody(Inventory inventory)
        {
            var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Thông báo tồn kho thấp</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #f44336; color: white; padding: 10px; text-align: center; }}
        .footer {{ background-color: #f1f1f1; padding: 10px; text-align: center; font-size: 12px; }}
        .product-info {{ background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin-bottom: 20px; }}
        .warning {{ color: #f44336; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Thông báo tồn kho thấp</h1>
        </div>
        <p>Kính gửi Quản trị viên,</p>
        <p>Hệ thống đã phát hiện một sản phẩm có tồn kho thấp dưới ngưỡng cảnh báo.</p>
        <div class='product-info'>
            <p><strong>Sản phẩm:</strong> {inventory.Product?.Name ?? "Không xác định"}</p>
            <p><strong>Mã sản phẩm:</strong> {inventory.Product?.Id ?? 0}</p>
            <p><strong>Tồn kho hiện tại:</strong> <span class='warning'>{inventory.Quantity}</span></p>
            <p><strong>Ngưỡng cảnh báo:</strong> {inventory.MinimumStockLevel}</p>
            <p><strong>Cập nhật lần cuối:</strong> {inventory.LastUpdated:dd/MM/yyyy HH:mm}</p>
        </div>
        <p>Vui lòng kiểm tra và bổ sung hàng tồn kho cho sản phẩm này để đảm bảo đáp ứng nhu cầu của khách hàng.</p>
        <p>Trân trọng,</p>
        <p>Hệ thống CPT Store</p>
        <div class='footer'>
            <p>© {DateTime.Now.Year} CPT Store. Tất cả các quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";

            return body;
        }

        private static string GenerateWelcomeEmailBody(string username)
        {
            var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Chào mừng đến với CPT Store</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 10px; text-align: center; }}
        .footer {{ background-color: #f1f1f1; padding: 10px; text-align: center; font-size: 12px; }}
        .button {{ display: inline-block; background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; margin-top: 20px; }}
        .features {{ display: flex; justify-content: space-between; margin: 20px 0; }}
        .feature {{ flex: 1; padding: 10px; text-align: center; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Chào mừng đến với CPT Store!</h1>
        </div>
        <p>Xin chào {username},</p>
        <p>Cảm ơn bạn đã đăng ký tài khoản tại CPT Store. Chúng tôi rất vui mừng được chào đón bạn tham gia cộng đồng của chúng tôi!</p>
        <p>Với tài khoản CPT Store, bạn có thể:</p>
        <div class='features'>
            <div class='feature'>
                <h3>Mua sắm dễ dàng</h3>
                <p>Dễ dàng tìm kiếm và mua sắm các sản phẩm yêu thích</p>
            </div>
            <div class='feature'>
                <h3>Theo dõi đơn hàng</h3>
                <p>Theo dõi trạng thái đơn hàng và lịch sử mua hàng</p>
            </div>
            <div class='feature'>
                <h3>Ưu đãi đặc biệt</h3>
                <p>Nhận thông báo về các ưu đãi và khuyến mãi đặc biệt</p>
            </div>
        </div>
        <p>Hãy bắt đầu khám phá các sản phẩm tuyệt vời của chúng tôi ngay bây giờ!</p>
        <a href='https://cptstore.com/products' class='button'>Khám phá sản phẩm</a>
        <p>Nếu bạn có bất kỳ câu hỏi nào, đừng ngần ngại liên hệ với đội ngũ hỗ trợ của chúng tôi qua email support@cptstore.com hoặc số điện thoại 1900 1234.</p>
        <p>Trân trọng,</p>
        <p>Đội ngũ CPT Store</p>
        <div class='footer'>
            <p>© {DateTime.Now.Year} CPT Store. Tất cả các quyền được bảo lưu.</p>
            <p>Nếu bạn không muốn nhận email từ chúng tôi, vui lòng <a href='https://cptstore.com/unsubscribe'>hủy đăng ký</a>.</p>
        </div>
    </div>
</body>
</html>";

            return body;
        }

        private static string GeneratePasswordResetEmailBody(string resetToken)
        {
            var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Đặt lại mật khẩu</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 10px; text-align: center; }}
        .footer {{ background-color: #f1f1f1; padding: 10px; text-align: center; font-size: 12px; }}
        .button {{ display: inline-block; background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; margin-top: 20px; }}
        .warning {{ color: #f44336; font-style: italic; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Đặt lại mật khẩu</h1>
        </div>
        <p>Xin chào,</p>
        <p>Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản CPT Store của bạn. Vui lòng nhấp vào nút bên dưới để đặt lại mật khẩu của bạn:</p>
        <a href='https://cptstore.com/reset-password?token={resetToken}' class='button'>Đặt lại mật khẩu</a>
        <p>Liên kết này sẽ hết hạn sau 24 giờ. Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này hoặc liên hệ với chúng tôi nếu bạn có câu hỏi.</p>
        <p class='warning'>Nếu bạn không thực hiện yêu cầu này, vui lòng liên hệ với bộ phận hỗ trợ khách hàng ngay lập tức.</p>
        <p>Trân trọng,</p>
        <p>Đội ngũ CPT Store</p>
        <div class='footer'>
            <p>© {DateTime.Now.Year} CPT Store. Tất cả các quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";

            return body;
        }

        private static string GenerateContactFormConfirmationEmailBody(string name, string message)
        {
            var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Xác nhận liên hệ</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 10px; text-align: center; }}
        .footer {{ background-color: #f1f1f1; padding: 10px; text-align: center; font-size: 12px; }}
        .message-box {{ background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Xác nhận liên hệ</h1>
        </div>
        <p>Xin chào {name},</p>
        <p>Cảm ơn bạn đã liên hệ với CPT Store. Chúng tôi đã nhận được tin nhắn của bạn và sẽ phản hồi trong thời gian sớm nhất.</p>
        <p>Dưới đây là nội dung tin nhắn bạn đã gửi:</p>
        <div class='message-box'>
            <p>{message}</p>
        </div>
        <p>Đội ngũ hỗ trợ khách hàng của chúng tôi sẽ xem xét tin nhắn của bạn và phản hồi trong vòng 24-48 giờ làm việc.</p>
        <p>Trân trọng,</p>
        <p>Đội ngũ CPT Store</p>
        <div class='footer'>
            <p>© {DateTime.Now.Year} CPT Store. Tất cả các quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";

            return body;
        }

        private static string GenerateContactFormNotificationEmailBody(string name, string email, string message)
        {
            var body = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Tin nhắn liên hệ mới</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 10px; text-align: center; }}
        .footer {{ background-color: #f1f1f1; padding: 10px; text-align: center; font-size: 12px; }}
        .message-box {{ background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0; }}
        .contact-info {{ margin-bottom: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Tin nhắn liên hệ mới</h1>
        </div>
        <p>Một khách hàng đã gửi tin nhắn liên hệ mới.</p>
        <div class='contact-info'>
            <p><strong>Tên:</strong> {name}</p>
            <p><strong>Email:</strong> {email}</p>
            <p><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</p>
        </div>
        <p><strong>Nội dung tin nhắn:</strong></p>
        <div class='message-box'>
            <p>{message}</p>
        </div>
        <p>Vui lòng phản hồi khách hàng trong thời gian sớm nhất.</p>
        <p>Trân trọng,</p>
        <p>Hệ thống CPT Store</p>
        <div class='footer'>
            <p>© {DateTime.Now.Year} CPT Store. Tất cả các quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";

            return body;
        }

        #endregion // Email Templates
    }
}
