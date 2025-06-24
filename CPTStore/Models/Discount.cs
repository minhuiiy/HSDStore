using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CPTStore.Models
{
    public enum DiscountType
    {
        Percentage,     // Giảm giá theo phần trăm
        FixedAmount     // Giảm giá theo số tiền cố định
    }

    public class Discount
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Mã giảm giá không được để trống")]
        [StringLength(50, ErrorMessage = "Mã giảm giá không được vượt quá 50 ký tự")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mô tả không được để trống")]
        [StringLength(200, ErrorMessage = "Mô tả không được vượt quá 200 ký tự")]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Giá trị giảm giá phải lớn hơn hoặc bằng 0")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Value { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá trị đơn hàng tối thiểu phải lớn hơn hoặc bằng 0")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal MinimumOrderAmount { get; set; } = 0;

        [Range(0, double.MaxValue, ErrorMessage = "Giá trị giảm tối đa phải lớn hơn hoặc bằng 0")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? MaximumDiscountAmount { get; set; }

        public DateTime StartDate { get; set; } = DateTime.Now;

        public DateTime? EndDate { get; set; }

        public int? UsageLimit { get; set; }

        public int UsageCount { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Kiểm tra xem mã giảm giá có còn hiệu lực không
        [NotMapped]
        public bool IsValid => IsActive && 
                             (EndDate == null || EndDate >= DateTime.Now) && 
                             (StartDate <= DateTime.Now) && 
                             (UsageLimit == null || UsageCount < UsageLimit);
    }
}