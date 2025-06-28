using System.ComponentModel.DataAnnotations;
using CPTStore.Models;
using System.Collections.Generic;

namespace CPTStore.ViewModels
{
    public class CheckoutViewModel
    {
        // Thông tin giao hàng
        [Required(ErrorMessage = "Vui lòng nhập họ")]
        [Display(Name = "Họ")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên")]
        [Display(Name = "Tên")]
        public string LastName { get; set; } = string.Empty;
        
        // Thuộc tính tính toán để tương thích ngược
        public string FullName => $"{FirstName} {LastName}".Trim();

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ")]
        [Display(Name = "Địa chỉ")]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "Thành phố")]
        [Required(ErrorMessage = "Vui lòng chọn thành phố")]
        public string City { get; set; } = string.Empty;

        [Display(Name = "Quận/Huyện")]
        [Required(ErrorMessage = "Vui lòng chọn quận/huyện")]
        public string District { get; set; } = string.Empty;
        
        [Display(Name = "Phường/Xã")]
        [Required(ErrorMessage = "Vui lòng chọn phường/xã")]
        public string Ward { get; set; } = string.Empty;

        [Display(Name = "Ghi chú")]
        public string? Notes { get; set; }
        
        // Alias for Notes to maintain compatibility
        [Display(Name = "Ghi chú")]
        public string? Note { get => Notes; set => Notes = value; }

        // Thông tin thanh toán
        
        [Display(Name = "Tổng tiền")]
        public decimal TotalAmount { get; set; }
        [Display(Name = "Phương thức thanh toán")]
        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
        public Models.PaymentMethod PaymentMethod { get; set; }

        // Thông tin đơn hàng
        public List<CartItem>? CartItems { get; set; }

        public decimal SubTotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal StandardShippingFee { get; set; } = 30000;
        public decimal ExpressShippingFee { get; set; } = 60000;
        public decimal Discount { get; set; }
        public decimal Total => SubTotal + ShippingFee - Discount;
    }
}