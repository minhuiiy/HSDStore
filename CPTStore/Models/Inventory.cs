using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CPTStore.Models
{
    public class Inventory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho phải lớn hơn hoặc bằng 0")]
        public int Quantity { get; set; } = 0;

        [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho tối thiểu phải lớn hơn hoặc bằng 0")]
        public int MinimumStockLevel { get; set; } = 5;

        [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho tối đa phải lớn hơn hoặc bằng 0")]
        public int MaximumStockLevel { get; set; } = 100;

        public DateTime LastRestockDate { get; set; } = DateTime.Now;

        public DateTime? LastStockOutDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        public DateTime LastUpdated => UpdatedAt ?? CreatedAt;

        // Kiểm tra xem sản phẩm có còn hàng không
        [NotMapped]
        public bool IsInStock => Quantity > 0;

        // Kiểm tra xem sản phẩm có cần nhập thêm hàng không
        [NotMapped]
        public bool NeedsRestock => Quantity <= MinimumStockLevel;

        // Thuộc tính tính toán để tương thích với mã hiện tại
        [NotMapped]
        public int StockQuantity => Quantity;

        [NotMapped]
        public int LowStockThreshold => MinimumStockLevel;
    }
}