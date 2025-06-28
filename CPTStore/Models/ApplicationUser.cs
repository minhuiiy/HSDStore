using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CPTStore.Models
{
    public enum MembershipLevel
    {
        Regular = 0,    // Thành viên thường
        Silver = 1,     // Thành viên bạc (trên 2.000.000 VNĐ)
        Gold = 2,       // Thành viên vàng (trên 5.000.000 VNĐ)
        Diamond = 3     // Thành viên kim cương (trên 10.000.000 VNĐ)
    }

    public class ApplicationUser : IdentityUser
    {
        [StringLength(100)]
        public string? FirstName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(20)]
        public string? PostalCode { get; set; }

        [StringLength(100)]
        public string? Country { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastLoginDate { get; set; }

        // Xác định vai trò admin
        public bool IsAdmin { get; set; }
        
        // Trạng thái hoạt động của tài khoản
        public bool IsActive { get; set; } = true;

        // Cấp độ thành viên
        public MembershipLevel MembershipLevel { get; set; } = MembershipLevel.Regular;

        // Tổng giá trị đơn hàng đã mua
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalPurchases { get; set; } = 0;

        // Navigation properties
        public ICollection<Order>? Orders { get; set; }
        public ICollection<ProductReview>? Reviews { get; set; }

        // Tên đầy đủ của người dùng
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}