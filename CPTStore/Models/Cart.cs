using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CPTStore.Models
{
    public class Cart
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

        [NotMapped]
        public decimal TotalAmount => CalculateTotalAmount();

        [NotMapped]
        public int TotalItems => CartItems?.Count ?? 0;
        
        [NotMapped]
        public decimal ShippingFee { get; set; } = 0;
        
        [NotMapped]
        public decimal Discount { get; set; } = 0;

        private decimal CalculateTotalAmount()
        {
            decimal total = 0;
            if (CartItems != null)
            {
                foreach (var item in CartItems)
                {
                    total += item.Quantity * (item.Product?.Price ?? 0);
                }
            }
            return total;
        }
    }
}