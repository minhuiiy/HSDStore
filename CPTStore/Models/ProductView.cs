using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CPTStore.Models
{
    public class ProductView
    {
        [Key]
        public int Id { get; set; }

        public string? UserId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [Required]
        public string IPAddress { get; set; } = string.Empty;

        public string? UserAgent { get; set; }

        public DateTime ViewedAt { get; set; } = DateTime.Now;

        // Thời gian xem sản phẩm (tính bằng giây)
        public int? ViewDuration { get; set; }
    }
}