using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CPTStore.Models
{
    public enum OrderStatus
    {
        Pending,        // Chờ xử lý
        Processing,     // Đang xử lý
        Shipped,        // Đã giao cho đơn vị vận chuyển
        Delivered,      // Đã giao hàng
        Cancelled,      // Đã hủy
        Refunded        // Đã hoàn tiền
    }

    public enum PaymentMethod
    {
        COD,            // Thanh toán khi nhận hàng
        CreditCard,     // Thẻ tín dụng
        BankTransfer,   // Chuyển khoản ngân hàng
        Momo,           // Ví điện tử Momo
        VNPay,          // Cổng thanh toán VNPay
        ZaloPay         // Ví điện tử ZaloPay
    }

    public enum PaymentStatus
    {
        Pending,        // Chờ thanh toán
        Completed,      // Đã thanh toán
        Failed,         // Thanh toán thất bại
        Refunded        // Đã hoàn tiền
    }

    public class Order
    {
        [Key]
        public int Id { get; set; }

        // Cho phép UserId là null để hỗ trợ đơn hàng của khách không đăng nhập
        public string? UserId { get; set; }
        
        // SessionId để lưu trữ ID phiên làm việc của khách không đăng nhập
        [StringLength(100)]
        public string? SessionId { get; set; }

        [Required]
        [StringLength(100)]
        public string OrderNumber { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string OrderReference { get; set; } = string.Empty;
        
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [Required]
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;

        [Required]
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal DiscountAmount { get; set; } = 0;
        
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShippingFee { get; set; } = 0;

        [StringLength(50)]
        public string? DiscountCode { get; set; }

        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Address { get; set; } = string.Empty;

        [StringLength(100)]
        public string? City { get; set; }
        
        [StringLength(100)]
        public string? District { get; set; }
        
        [StringLength(100)]
        public string? Ward { get; set; }

        [StringLength(20)]
        public string? PostalCode { get; set; }
        
        [StringLength(50)]
        public string? FirstName { get; set; }
        
        [StringLength(50)]
        public string? LastName { get; set; }
        
        [EmailAddress]
        [StringLength(100)]
        public string? Email { get; set; }
        
        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public string? TransactionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public IEnumerable<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public ApplicationUser? ApplicationUser { get; set; }

        // Thuộc tính tính toán để tương thích với mã hiện tại
        [NotMapped]
        public string CustomerPhone => PhoneNumber;

        [NotMapped]
        public string CustomerAddress => Address;
        
        // Thuộc tính tính toán cho SubTotal, ShippingFee, Discount và Note
        [NotMapped]
        public decimal SubTotal => OrderItems?.Sum(item => item.Price * item.Quantity) ?? 0;
        
        // ShippingFee is now a real property, no need for NotMapped version
        
        [NotMapped]
        public decimal Discount => DiscountAmount;
        
        [NotMapped]
        public string? Note => Notes;
        
        [NotMapped]
        public string? CustomerEmail => Email;
        
        [NotMapped]
        public string? ShippingAddress => Address;
        
        [NotMapped]
        public bool IsPaid => PaymentStatus == PaymentStatus.Completed;
        
        [NotMapped]
        public string? TrackingNumber { get; set; }
    }
}