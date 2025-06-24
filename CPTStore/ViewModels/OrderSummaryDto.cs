using CPTStore.Models;
using System;

namespace CPTStore.ViewModels
{
    /// <summary>
    /// DTO cho thông tin tóm tắt đơn hàng, sử dụng cho projection để tối ưu hóa truy vấn
    /// </summary>
    public class OrderSummaryDto
    {
        /// <summary>
        /// ID của đơn hàng
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Mã đơn hàng
        /// </summary>
        public string OrderNumber { get; set; } = string.Empty;

        /// <summary>
        /// Tên khách hàng
        /// </summary>
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>
        /// Số điện thoại
        /// </summary>
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// Tổng tiền đơn hàng
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Trạng thái đơn hàng
        /// </summary>
        public OrderStatus Status { get; set; }

        /// <summary>
        /// Trạng thái thanh toán
        /// </summary>
        public PaymentStatus PaymentStatus { get; set; }

        /// <summary>
        /// Thời gian tạo đơn hàng
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Số lượng sản phẩm trong đơn hàng
        /// </summary>
        public int ItemCount { get; set; }
    }
}