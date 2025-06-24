using System;
using CPTStore.Models;

namespace CPTStore.ViewModels
{
    /// <summary>
    /// ViewModel cho CartItem, sử dụng để hiển thị thông tin giỏ hàng
    /// </summary>
    public class CartItemViewModel
    {
        /// <summary>
        /// ID của CartItem
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID của sản phẩm
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// Tên sản phẩm
        /// </summary>
        public required string ProductName { get; set; }

        /// <summary>
        /// URL hình ảnh sản phẩm
        /// </summary>
        public required string ImageUrl { get; set; }

        /// <summary>
        /// Số lượng sản phẩm
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Giá sản phẩm
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Tổng giá (Giá * Số lượng)
        /// </summary>
        public decimal TotalPrice { get; set; }

        /// <summary>
        /// Ngày tạo
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Ngày cập nhật
        /// </summary>
        public DateTime UpdatedAt { get; set; }
        
        /// <summary>
        /// Đối tượng Product liên kết với CartItem
        /// </summary>
        public Product? Product { get; set; }
    }
}