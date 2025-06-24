using CPTStore.Models;
using System;

namespace CPTStore.ViewModels
{
    /// <summary>
    /// ViewModel cho Order, sử dụng để hiển thị thông tin đơn hàng
    /// </summary>
    public class OrderViewModel
    {
        /// <summary>
        /// ID của đơn hàng
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID của người dùng
        /// </summary>
        public required string UserId { get; set; }

        /// <summary>
        /// Tên người nhận
        /// </summary>
        public required string RecipientName { get; set; }

        /// <summary>
        /// Địa chỉ giao hàng
        /// </summary>
        public required string ShippingAddress { get; set; }

        /// <summary>
        /// Số điện thoại
        /// </summary>
        public required string PhoneNumber { get; set; }

        /// <summary>
        /// Email
        /// </summary>
        public required string Email { get; set; }

        /// <summary>
        /// Tổng tiền đơn hàng
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Phí vận chuyển
        /// </summary>
        public decimal ShippingFee { get; set; }

        /// <summary>
        /// Giảm giá
        /// </summary>
        public decimal Discount { get; set; }

        /// <summary>
        /// Trạng thái đơn hàng
        /// </summary>
        public OrderStatus OrderStatus { get; set; }

        /// <summary>
        /// Tên trạng thái đơn hàng
        /// </summary>
        public required string OrderStatusName { get; set; }

        /// <summary>
        /// Phương thức thanh toán
        /// </summary>
        public PaymentMethod PaymentMethod { get; set; }

        /// <summary>
        /// Tên phương thức thanh toán
        /// </summary>
        public required string PaymentMethodName { get; set; }

        /// <summary>
        /// Ghi chú đơn hàng
        /// </summary>
        public required string Note { get; set; }

        /// <summary>
        /// Ngày tạo
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Ngày cập nhật
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}