using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CPTStore.Services
{
    public class PaymentService(ApplicationDbContext context, IOrderService orderService, IEmailService emailService) : IPaymentService
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IOrderService _orderService = orderService;
        private readonly IEmailService _emailService = emailService;

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
            bool paymentSuccess;
            string? transactionId = null;
            string? redirectUrl = null;

            switch (paymentMethodEnum)
            {
                case PaymentMethod.COD:
                    // Thanh toán COD luôn được chấp nhận, nhưng trạng thái vẫn là Pending
                    paymentSuccess = true;
                    break;

                case PaymentMethod.CreditCard:
                    paymentSuccess = await ProcessCreditCardPaymentAsync(null);
                    redirectUrl = GenerateCreditCardPaymentUrl(order);
                    break;

                case PaymentMethod.BankTransfer:
                    // Đối với chuyển khoản ngân hàng, cần xác minh thủ công
                    // Trạng thái thanh toán vẫn là Pending cho đến khi xác minh
                    paymentSuccess = true;
                    break;

                case PaymentMethod.Momo:
                    paymentSuccess = await ProcessMomoPaymentAsync(null);
                    redirectUrl = await GenerateMomoPaymentUrlAsync(order);
                    break;

                case PaymentMethod.VNPay:
                    paymentSuccess = await ProcessVNPayPaymentAsync(null);
                    redirectUrl = await GenerateVNPayPaymentUrlAsync(order);
                    break;

                case PaymentMethod.ZaloPay:
                    paymentSuccess = await ProcessZaloPayPaymentAsync(null);
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
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"Lỗi cơ sở dữ liệu khi xử lý thanh toán cho đơn hàng ID: {orderId}, Lỗi: {dbEx.Message}");
                if (dbEx.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
                throw new InvalidOperationException($"Không thể xử lý thanh toán do lỗi cơ sở dữ liệu: {dbEx.Message}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xử lý thanh toán cho đơn hàng ID: {orderId}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Không thể xử lý thanh toán: {ex.Message}", ex);
            }

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
            bool verificationSuccess;

            switch (paymentMethodEnum)
            {
                case PaymentMethod.BankTransfer:
                    verificationSuccess = await VerifyBankTransferAsync(order, transactionId);
                    break;

                case PaymentMethod.CreditCard:
                    verificationSuccess = await VerifyCreditCardPaymentAsync();
                    break;

                case PaymentMethod.Momo:
                    verificationSuccess = await VerifyMomoPaymentAsync();
                    break;

                case PaymentMethod.VNPay:
                    verificationSuccess = await VerifyVNPayPaymentAsync();
                    break;

                case PaymentMethod.ZaloPay:
                    verificationSuccess = await VerifyZaloPayPaymentAsync();
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
                
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException dbEx)
                {
                    Console.WriteLine($"Lỗi cơ sở dữ liệu khi xác minh thanh toán cho đơn hàng ID: {orderId}, Lỗi: {dbEx.Message}");
                    if (dbEx.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
                    throw new InvalidOperationException($"Không thể xác minh thanh toán do lỗi cơ sở dữ liệu: {dbEx.Message}", dbEx);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi khi xác minh thanh toán cho đơn hàng ID: {orderId}, Lỗi: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    throw new InvalidOperationException($"Không thể xác minh thanh toán: {ex.Message}", ex);
                }

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
            string refundMessage;

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
                
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException dbEx)
                {
                    Console.WriteLine($"Lỗi cơ sở dữ liệu khi hoàn tiền cho đơn hàng ID: {orderId}, Lỗi: {dbEx.Message}");
                    if (dbEx.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
                    throw new InvalidOperationException($"Không thể hoàn tiền do lỗi cơ sở dữ liệu: {dbEx.Message}", dbEx);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi khi hoàn tiền cho đơn hàng ID: {orderId}, Lỗi: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    throw new InvalidOperationException($"Không thể hoàn tiền: {ex.Message}", ex);
                }

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

            string paymentUrl;

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
                paymentUrl += (paymentUrl.Contains('?') ? "&" : "?") + "returnUrl=" + Uri.EscapeDataString(returnUrl);
            }

            return paymentUrl;
        }

        #region Private Methods for Payment Processing

        private static async Task<bool> ProcessCreditCardPaymentAsync(string? transactionId)
        {
            // Mô phỏng xử lý thanh toán thẻ tín dụng
            // Trong thực tế, bạn sẽ tích hợp với cổng thanh toán thẻ tín dụng
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return !string.IsNullOrEmpty(transactionId);
        }

        private static async Task<bool> ProcessMomoPaymentAsync(string? transactionId)
        {
            // Mô phỏng xử lý thanh toán Momo
            // Trong thực tế, bạn sẽ tích hợp với API của Momo
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return !string.IsNullOrEmpty(transactionId);
        }

        private static async Task<bool> ProcessVNPayPaymentAsync(string? transactionId)
        {
            // Mô phỏng xử lý thanh toán VNPay
            // Trong thực tế, bạn sẽ tích hợp với API của VNPay
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return !string.IsNullOrEmpty(transactionId);
        }

        private static async Task<bool> ProcessZaloPayPaymentAsync(string? transactionId)
        {
            // Mô phỏng xử lý thanh toán ZaloPay
            // Trong thực tế, bạn sẽ tích hợp với API của ZaloPay
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return !string.IsNullOrEmpty(transactionId);
        }

        private static async Task<bool> VerifyBankTransferAsync(Order order, string transactionId)
        {
            // Mô phỏng xác minh chuyển khoản ngân hàng
            // Trong thực tế, bạn sẽ kiểm tra thông tin chuyển khoản
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private static async Task<bool> VerifyCreditCardPaymentAsync()
        {
            // Mô phỏng xác minh thanh toán thẻ tín dụng
            // Trong thực tế, bạn sẽ tích hợp với cổng thanh toán thẻ tín dụng
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private static async Task<bool> VerifyMomoPaymentAsync()
        {
            // Mô phỏng xác minh thanh toán Momo
            // Trong thực tế, bạn sẽ tích hợp với API của Momo
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private static async Task<bool> VerifyVNPayPaymentAsync()
        {
            // Mô phỏng xác minh thanh toán VNPay
            // Trong thực tế, bạn sẽ tích hợp với API của VNPay
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private static async Task<bool> VerifyZaloPayPaymentAsync()
        {
            // Mô phỏng xác minh thanh toán ZaloPay
            // Trong thực tế, bạn sẽ tích hợp với API của ZaloPay
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private static async Task<bool> RefundCreditCardPaymentAsync(Order order)
        {
            // Mô phỏng hoàn tiền thẻ tín dụng
            // Trong thực tế, bạn sẽ tích hợp với cổng thanh toán thẻ tín dụng
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private static async Task<bool> RefundMomoPaymentAsync(Order order)
        {
            // Mô phỏng hoàn tiền Momo
            // Trong thực tế, bạn sẽ tích hợp với API của Momo
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private static async Task<bool> RefundVNPayPaymentAsync(Order order)
        {
            // Mô phỏng hoàn tiền VNPay
            // Trong thực tế, bạn sẽ tích hợp với API của VNPay
            await Task.Delay(100); // Mô phỏng xử lý không đồng bộ
            return true;
        }

        private static async Task<bool> RefundZaloPayPaymentAsync(Order order)
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