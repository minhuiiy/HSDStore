using System;

namespace CPTStore.ViewModels
{
    /// <summary>
    /// DTO cho Category, sử dụng để ánh xạ dữ liệu từ Model sang ViewModel
    /// </summary>
    public class CategoryDto
    {
        /// <summary>
        /// ID của danh mục
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Tên danh mục
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Mô tả danh mục
        /// </summary>
        public string? Description { get; set; }

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