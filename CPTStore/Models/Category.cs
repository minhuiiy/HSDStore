using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CPTStore.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [StringLength(100, ErrorMessage = "Tên danh mục không được vượt quá 100 ký tự")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự")]
        public string? Description { get; set; }

        [StringLength(200)]
        public string? Slug { get; set; }

        [StringLength(255)]
        public string? ImageUrl { get; set; }

        public int? ParentCategoryId { get; set; }

        public Category? ParentCategory { get; set; }

        public ICollection<Category>? SubCategories { get; set; }

        public ICollection<Product>? Products { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }
        
        // Thứ tự hiển thị của danh mục
        public int DisplayOrder { get; set; } = 0;
        
        // Trạng thái hiển thị của danh mục
        public bool IsActive { get; set; } = true;
        
        // Danh mục có được đánh dấu là nổi bật không
        public bool IsFeatured { get; set; } = false;
        
        // Thuộc tính tính toán để hiển thị số lượng sản phẩm trong danh mục
        [NotMapped]
        public int ProductCount => Products?.Count ?? 0;
    }
}