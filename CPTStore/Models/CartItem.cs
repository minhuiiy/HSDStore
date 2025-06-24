using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CPTStore.Models
{
    public class CartItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
        public int Quantity { get; set; } = 1;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal UnitPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Thuộc tính tính toán để lấy URL hình ảnh từ sản phẩm
        [NotMapped]
        public string? ImageUrl => Product?.ImageUrl;

        // Thuộc tính tính toán để lấy tên sản phẩm
        [NotMapped]
        public string? ProductName => Product?.Name;
        
        // Thuộc tính tính toán để lấy giá sản phẩm
        [NotMapped]
        public decimal Price => UnitPrice;

        // Thuộc tính TotalPrice được chuyển sang CartItemExtensions.GetTotalPrice()
    }
}