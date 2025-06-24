using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CPTStore.Models
{
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

        // Navigation properties
        public ICollection<Order>? Orders { get; set; }
        public ICollection<ProductReview>? Reviews { get; set; }

        // Tên đầy đủ của người dùng
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}