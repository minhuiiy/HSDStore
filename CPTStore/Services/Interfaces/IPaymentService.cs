namespace CPTStore.Services
{
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string? TransactionId { get; set; }
        public string? Message { get; set; }
        public string? RedirectUrl { get; set; }
        public string? RefundReason { get; set; }
    }

    public interface IPaymentService
    {
        Task<PaymentResult> ProcessPaymentAsync(int orderId, string paymentMethod, string? returnUrl = null);
        Task<PaymentResult> VerifyPaymentAsync(int orderId, string paymentMethod, string transactionId);
        Task<string> GeneratePaymentUrlAsync(int orderId, string paymentMethod, string returnUrl);
        Task<PaymentResult> RefundPaymentAsync(int orderId, string? reason = null);
    }
}