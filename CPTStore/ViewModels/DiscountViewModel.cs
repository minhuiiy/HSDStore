using CPTStore.Models;
using System.Collections.Generic;

namespace CPTStore.ViewModels
{
    public class DiscountViewModel
    {
        public required IEnumerable<MembershipDiscount> MembershipDiscounts { get; set; }
        public required IEnumerable<Discount> GeneralDiscounts { get; set; }
        public required IEnumerable<SavedDiscount> SavedDiscounts { get; set; }
        public MembershipLevel MembershipLevel { get; set; }
        public decimal TotalPurchases { get; set; }
    }
}