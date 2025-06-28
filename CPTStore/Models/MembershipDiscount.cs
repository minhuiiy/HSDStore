using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CPTStore.Models
{
    public class MembershipDiscount
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public MembershipLevel MembershipLevel { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DiscountType DiscountType { get; set; } = DiscountType.FixedAmount;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Value { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? MaximumDiscountAmount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal MinimumOrderAmount { get; set; } = 0;

        [Required]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }
    }
}