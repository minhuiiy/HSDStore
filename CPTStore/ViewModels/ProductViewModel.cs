using System;

namespace CPTStore.ViewModels
{
    /// <summary>
    /// ViewModel cho Product, sử dụng để hiển thị thông tin sản phẩm
    /// </summary>
    public class ProductViewModel
    {
        /// <summary>
        /// ID của sản phẩm
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Tên sản phẩm
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Mô tả sản phẩm
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// Giá sản phẩm
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Số lượng tồn kho
        /// </summary>
        public int StockQuantity { get; set; }

        /// <summary>
        /// URL hình ảnh sản phẩm
        /// </summary>
        public required string ImageUrl { get; set; }

        /// <summary>
        /// ID của danh mục
        /// </summary>
        public int CategoryId { get; set; }

        /// <summary>
        /// Tên danh mục
        /// </summary>
        public required string CategoryName { get; set; }

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