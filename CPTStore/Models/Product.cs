using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CPTStore.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên sản phẩm không được để trống")]
        [StringLength(200, ErrorMessage = "Tên sản phẩm không được vượt quá 200 ký tự")]
        public string Name { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Mã sản phẩm không được vượt quá 50 ký tự")]
        public string SKU { get; set; } = string.Empty;

        [Required(ErrorMessage = "Giá sản phẩm không được để trống")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá sản phẩm phải lớn hơn hoặc bằng 0")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Price { get; set; }

        [StringLength(2000, ErrorMessage = "Mô tả không được vượt quá 2000 ký tự")]
        public string? Description { get; set; }

        [StringLength(500, ErrorMessage = "Mô tả ngắn không được vượt quá 500 ký tự")]
        public string? ShortDescription { get; set; }

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        public int Stock { get; set; } = 0;

        public bool IsAvailable { get; set; } = true;
        
        // Thuộc tính bổ sung cho trang Search.cshtml
        [NotMapped]
        public bool IsNew => (DateTime.Now - CreatedAt).TotalDays <= 7;
        
        [NotMapped]
        public decimal Discount { get; set; } = 0;
        
        [NotMapped]
        public int DiscountPercent => Price > 0 ? (int)(Discount / Price * 100) : 0;
        
        [NotMapped]
        public string CategoryName => Category?.Name ?? "Không phân loại";
        
        [NotMapped]
        public decimal OriginalPrice => Price + Discount;
        
        [NotMapped]
        public decimal Rating { get; set; } = 0;
        
        [NotMapped]
        public int ReviewCount => Reviews?.Count ?? 0;
        
        [NotMapped]
        public int StockQuantity => Stock;
        
        [NotMapped]
        public int SoldQuantity { get; set; } = 0;

        public int ViewCount { get; set; } = 0;

        [StringLength(200)]
        public string? Slug { get; set; }

        [StringLength(500)]
        public string? MetaDescription { get; set; }

        [StringLength(100)]
        public string? MetaKeywords { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public ICollection<OrderItem>? OrderItems { get; set; }
        public ICollection<CartItem>? CartItems { get; set; }
        public ICollection<ProductReview>? Reviews { get; set; }
    }
}