using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CPTStore.Models
{
    public class SavedDiscount
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int DiscountId { get; set; }

        [Required]
        public string DiscountCode { get; set; } = string.Empty;

        public bool IsUsed { get; set; } = false;

        public DateTime? UsedDate { get; set; }

        public DateTime SavedDate { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("DiscountId")]
        public Discount? Discount { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
    }
}