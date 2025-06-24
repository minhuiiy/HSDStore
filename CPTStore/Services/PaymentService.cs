using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CPTStore.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IOrderService _orderService;
        private readonly IEmailService _emailService;

        public PaymentService(ApplicationDbContext context, IOrderService orderService, IEmailService emailService)
        {
            _context = context;
            _orderService = orderService;
            _emailService = emailService;
        }

        public async Task<PaymentResult> ProcessPaymentAsync(int orderId, string paymentMethod, string? returnUrl = null)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Đơn hàng không tồn tại"
                };
            }

            // Chuyển đổi string paymentMethod thành enum PaymentMethod
            if (!Enum.TryParse<PaymentMethod>(paymentMethod, true, out var paymentMethodEnum))
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Phương thức thanh toán không hợp lệ"
                };
            }

            // Cập nhật phương thức thanh toán
            order.PaymentMethod = paymentMethodEnum;

            // Xử lý thanh toán dựa trên phương thức
            bool paymentSuccess = false;
            string? transactionId = null;
            string? redirectUrl = null;

            switch (paymentMethodEnum)
            {
                case PaymentMethod.COD:
                    // Thanh toán COD luôn được chấp nhận, nhưng trạng thái vẫn là Pending
                    paymentSuccess = true;
                    break;

                case PaymentMethod.CreditCard:
                    paymentSuccess = await ProcessCreditCardPaymentAsync(order, null);
                    redirectUrl = GenerateCreditCardPaymentUrl(order);
                    break;

                case PaymentMethod.BankTransfer:
                    // Đối với chuyển khoản ngân hàng, cần xác minh thủ công
                    // Trạng thái thanh toán vẫn là Pending cho đến khi xác minh
                    paymentSuccess = true;
                    break;

                case PaymentMethod.Momo:
                    paymentSuccess = await ProcessMomoPaymentAsync(order, null);
                    redirectUrl = await GenerateMomoPaymentUrlAsync(order);
                    break;

                case PaymentMethod.VNPay:
                    paymentSuccess = await ProcessVNPayPaymentAsync(order, null);
                    redirectUrl = await GenerateVNPayPaymentUrlAsync(order);
                    break;

                case PaymentMethod.ZaloPay:
                    paymentSuccess = await ProcessZaloPayPaymentAsync(order, null);
                    redirectUrl = await GenerateZaloPayPaymentUrlAsync(order);
                    break;

                default:
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Phương thức thanh toán không được hỗ trợ"
                    };
            }

            // Nếu thanh toán thành công và không phải COD hoặc chuyển khoản ngân hàng
            // thì cập nhật trạng thái thanh toán thành Completed
            if (paymentSuccess && paymentMethodEnum != PaymentMethod.COD && paymentMethodEnum != PaymentMethod.BankTransfer)
            {
                await _orderService.UpdatePaymentStatusAsync(orderId, PaymentStatus.Completed, transactionId);
            }

            // Lưu thay đổi
            await _context.SaveChangesAsync();

            // Gửi email xác nhận thanh toán nếu cần
            // Đã bỏ gọi SendPaymentConfirmationAsync vì IEmailService không có phương thức này

            return new PaymentResult
            {
                Success = paymentSuccess,
                TransactionId = transactionId,
                RedirectUrl = redirectUrl,
                Message = paymentSuccess ? "Thanh toán thành công" : "Thanh toán thất bại"
            };
        }

        public async Task<PaymentResult> VerifyPaymentAsync(int orderId, string paymentMethod, string transactionId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Đơn hàng không tồn tại"
                };
            }

            // Kiểm tra transactionId
            if (string.IsNullOrEmpty(transactionId))
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Mã giao dịch không hợp lệ"
                };
            }

            // Chuyển đổi string paymentMethod thành enum PaymentMethod
            if (!Enum.TryParse<PaymentMethod>(paymentMethod, true, out var paymentMethodEnum))
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Phương thức thanh toán không hợp lệ"
                };
            }

            // Xác minh thanh toán dựa trên phương thức thanh toán
            bool verificationSuccess = false;

            switch (paymentMethodEnum)
            {
                case PaymentMethod.BankTransfer:
                    verificationSuccess = await VerifyBankTransferAsync(order, transactionId);
                    break;

                case PaymentMethod.CreditCard:
                    verificationSuccess = await VerifyCreditCardPaymentAsync(order, transactionId);
                    break;

                case PaymentMethod.Momo:
                    verificationSuccess = await VerifyMomoPaymentAsync(order, transactionId);
                    break;

                case PaymentMethod.VNPay:
                    verificationSuccess = await VerifyVNPayPaymentAsync(order, transactionId);
                    break;

                case PaymentMethod.ZaloPay:
                    verificationSuccess = await VerifyZaloPayPaymentAsync(order, transactionId);
                    break;

                default:
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Phương thức thanh toán không được hỗ trợ"
                    };
            }

            // Nếu xác minh thành công, cập nhật trạng thái thanh toán
            if (verificationSuccess)
            {
                await _orderService.UpdatePaymentStatusAsync(orderId, PaymentStatus.Completed, transactionId);
                await _context.SaveChangesAsync();

                // Gửi email xác nhận đơn hàng thay vì email xác nhận thanh toán
                await _emailService.SendOrderConfirmationAsync(orderId);
            }

            return new PaymentResult
            {
                Success = verificationSuccess,
                TransactionId = transactionId,
                Message = verificationSuccess ? "Xác minh thanh toán thành công" : "Xác minh thanh toán thất bại"
            };
        }

        public async Task<PaymentResult> RefundPaymentAsync(int orderId, string? reason = null)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Đơn hàng không tồn tại"
                };
            }

            if (order.PaymentStatus != PaymentStatus.Completed)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Đơn hàng chưa được thanh toán hoặc đã được hoàn tiền"
                };
            }

            // Xử lý hoàn tiền dựa trên phương thức thanh toán
            bool refundSuccess = false;
            string refundMessage = "Hoàn tiền thất bại";

            switch (order.PaymentMethod)
            {
                case PaymentMethod.CreditCard:
                    refundSuccess = await RefundCreditCardPaymentAsync(order);
                    refundMessage = refundSuccess ? "Đã hoàn tiền vào thẻ tín dụng" : "Không thể hoàn tiền vào thẻ tín dụng";
                    break;

                case PaymentMethod.Momo:
                    refundSuccess = await RefundMomoPaymentAsync(order);
                    refundMessage = refundSuccess ? "Đã hoàn tiền vào ví Momo" : "Không thể hoàn tiền vào ví Momo";
                    break;

                case PaymentMethod.VNPay:
                    refundSuccess = await RefundVNPayPaymentAsync(order);
                    refundMessage = refundSuccess ? "Đã hoàn tiền qua VNPay" : "Không thể hoàn tiền qua VNPay";
                    break;

                case PaymentMethod.ZaloPay:
                    refundSuccess = await RefundZaloPayPaymentAsync(order);
                    refundMessage = refundSuccess ? "Đã hoàn tiền vào ví ZaloPay" : "Không thể hoàn tiền vào ví ZaloPay";
                    break;

                case PaymentMethod.BankTransfer:
                    // Đối với chuyển khoản ngân hàng, cần xử lý thủ công
                    refundSuccess = true;
                    refundMessage = "Yêu cầu hoàn tiền qua chuyển khoản ngân hàng đã được ghi nhận";
                    break;

                case PaymentMethod.COD:
                    // Nếu đơn hàng chưa giao, không cần hoàn tiền
                    if (order.Status != OrderStatus.Delivered)
                    {
                        refundSuccess = true;
                        refundMessage = "Đơn hàng COD chưa giao, đã hủy thành công";
                    }
                    else
                    {
                        refundMessage = "Đơn hàng COD đã giao, cần liên hệ để hoàn tiền";
                    }
                    break;

                default:
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Phương thức thanh toán không được hỗ trợ"
                    };
            }

            // Nếu hoàn tiền thành công, cập nhật trạng thái thanh toán
            if (refundSuccess)
            {
                await _orderService.UpdatePaymentStatusAsync(orderId, PaymentStatus.Refunded);
                await _context.SaveChangesAsync();

                // Gửi email xác nhận cập nhật trạng thái đơn hàng thay vì email xác nhận hoàn tiền
                await _emailService.SendOrderStatusUpdateAsync(orderId);
            }

            return new PaymentResult
            {
                Success = refundSuccess,
                Message = refundMessage,
                RefundReason = reason
            };
        }

        public async Task<string> GeneratePaymentUrlAsync(int orderId, string paymentMethod, string returnUrl)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return string.Empty;
            }

            // Chuyển đổi string paymentMethod thành enum PaymentMethod
            if (!Enum.TryParse<PaymentMethod>(paymentMethod, true, out var paymentMethodEnum))
            {
                return string.Empty;
            }

            string paymentUrl = string.Empty;

            switch (paymentMethodEnum)
            {
                case PaymentMethod.CreditCard:
                    paymentUrl = GenerateCreditCardPaymentUrl(order);
                    break;

                case PaymentMethod.Momo:
                    paymentUrl = await GenerateMomoPaymentUrlAsync(order);
                    break;

                case PaymentMethod.VNPay:
                    paymentUrl = await GenerateVNPayPaymentUrlAsync(order);
                    break;

                case PaymentMethod.ZaloPay:
                    paymentUrl = await GenerateZaloPayPaymentUrlAsync(order);
                    break;

                default:
                    return string.Empty;
            }
            
            // Thêm returnUrl vào URL thanh toán nếu được cung cấp
            if (!string.IsNullOrEmpty(returnUrl) && !string.IsNullOrEmpty(paymentUrl))
            {
                // Thêm tham số returnUrl vào URL thanh toán
                paymentUrl += (paymentUrl.Contains("?") ? "&" : "?") + "returnUrl=" + Uri.EscapeDataString(returnUrl);
            }

            return paymentUrl;
        }

        #region Private Methods for Payment Processing

        private async Task<bool> ProcessCreditCardPaymentAsync(Order order, string? transactionId)
        {
            // Mô phỏng xử lý thanh toán thẻ tín dụng
            // Trong thực tế, bạn sẽ tích hợp với cổng thanh toán thẻ tín dụng
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return !string.IsNullOrEmpty(transactionId);
        }

        private async Task<bool> ProcessMomoPaymentAsync(Order order, string? transactionId)
        {
            // Mô phỏng xử lý thanh toán Momo
            // Trong thực tế, bạn sẽ tích hợp với API của Momo
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return !string.IsNullOrEmpty(transactionId);
        }

        private async Task<bool> ProcessVNPayPaymentAsync(Order order, string? transactionId)
        {
            // Mô phỏng xử lý thanh toán VNPay
            // Trong thực tế, bạn sẽ tích hợp với API của VNPay
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return !string.IsNullOrEmpty(transactionId);
        }

        private async Task<bool> ProcessZaloPayPaymentAsync(Order order, string? transactionId)
        {
            // Mô phỏng xử lý thanh toán ZaloPay
            // Trong thực tế, bạn sẽ tích hợp với API của ZaloPay
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return !string.IsNullOrEmpty(transactionId);
        }

        private async Task<bool> VerifyBankTransferAsync(Order order, string transactionId)
        {
            // Mô phỏng xác minh chuyển khoản ngân hàng
            // Trong thực tế, bạn sẽ kiểm tra thông tin chuyển khoản
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private async Task<bool> VerifyCreditCardPaymentAsync(Order order, string transactionId)
        {
            // Mô phỏng xác minh thanh toán thẻ tín dụng
            // Trong thực tế, bạn sẽ tích hợp với cổng thanh toán thẻ tín dụng
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private async Task<bool> VerifyMomoPaymentAsync(Order order, string transactionId)
        {
            // Mô phỏng xác minh thanh toán Momo
            // Trong thực tế, bạn sẽ tích hợp với API của Momo
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private async Task<bool> VerifyVNPayPaymentAsync(Order order, string transactionId)
        {
            // Mô phỏng xác minh thanh toán VNPay
            // Trong thực tế, bạn sẽ tích hợp với API của VNPay
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private async Task<bool> VerifyZaloPayPaymentAsync(Order order, string transactionId)
        {
            // Mô phỏng xác minh thanh toán ZaloPay
            // Trong thực tế, bạn sẽ tích hợp với API của ZaloPay
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private async Task<bool> RefundCreditCardPaymentAsync(Order order)
        {
            // Mô phỏng hoàn tiền thẻ tín dụng
            // Trong thực tế, bạn sẽ tích hợp với cổng thanh toán thẻ tín dụng
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private async Task<bool> RefundMomoPaymentAsync(Order order)
        {
            // Mô phỏng hoàn tiền Momo
            // Trong thực tế, bạn sẽ tích hợp với API của Momo
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private async Task<bool> RefundVNPayPaymentAsync(Order order)
        {
            // Mô phỏng hoàn tiền VNPay
            // Trong thực tế, bạn sẽ tích hợp với API của VNPay
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private async Task<bool> RefundZaloPayPaymentAsync(Order order)
        {
            // Mô phỏng hoàn tiền ZaloPay
            // Trong thực tế, bạn sẽ tích hợp với API của ZaloPay
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private string GenerateCreditCardPaymentUrl(Order order)
        {
            // Mô phỏng tạo URL thanh toán thẻ tín dụng
            // Trong thực tế, bạn sẽ tích hợp với cổng thanh toán thẻ tín dụng
            var baseUrl = "https://example.com/payment/credit-card";
            var parameters = new StringBuilder()
                .Append($"?orderId={order.Id}")
                .Append($"&amount={order.TotalAmount}")
                .Append($"&orderNumber={order.OrderNumber}");

            return baseUrl + parameters.ToString();
        }

        private async Task<string> GenerateMomoPaymentUrlAsync(Order order)
        {
            // Mô phỏng tạo URL thanh toán Momo
            // Trong thực tế, bạn sẽ tích hợp với API của Momo
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ

            var baseUrl = "https://example.com/payment/momo";
            var parameters = new StringBuilder()
                .Append($"?orderId={order.Id}")
                .Append($"&amount={order.TotalAmount}")
                .Append($"&orderNumber={order.OrderNumber}");

            return baseUrl + parameters.ToString();
        }

        private async Task<string> GenerateVNPayPaymentUrlAsync(Order order)
        {
            // Mô phỏng tạo URL thanh toán VNPay
            // Trong thực tế, bạn sẽ tích hợp với API của VNPay
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ

            var baseUrl = "https://example.com/payment/vnpay";
            var parameters = new StringBuilder()
                .Append($"?orderId={order.Id}")
                .Append($"&amount={order.TotalAmount}")
                .Append($"&orderNumber={order.OrderNumber}");

            return baseUrl + parameters.ToString();
        }

        private async Task<string> GenerateZaloPayPaymentUrlAsync(Order order)
        {
            // Mô phỏng tạo URL thanh toán ZaloPay
            // Trong thực tế, bạn sẽ tích hợp với API của ZaloPay
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ

            var baseUrl = "https://example.com/payment/zalopay";
            var parameters = new StringBuilder()
                .Append($"?orderId={order.Id}")
                .Append($"&amount={order.TotalAmount}")
                .Append($"&orderNumber={order.OrderNumber}");

            return baseUrl + parameters.ToString();
        }

        #endregion
    }
}