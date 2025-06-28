using CPTStore.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CPTStore.Services.Interfaces
{
    public interface IMembershipService
    {
        // Phương thức cập nhật cấp độ thành viên dựa trên tổng giá trị đơn hàng
        Task UpdateMembershipLevelAsync(string userId, decimal orderAmount);
        
        // Phương thức lấy cấp độ thành viên hiện tại
        Task<MembershipLevel> GetMembershipLevelAsync(string userId);
        
        // Phương thức lấy tổng giá trị đơn hàng đã mua
        Task<decimal> GetTotalPurchasesAsync(string userId);
        
        // Phương thức lấy danh sách mã giảm giá theo cấp độ thành viên
        Task<IEnumerable<MembershipDiscount>> GetMembershipDiscountsAsync(MembershipLevel level);
        
        // Phương thức lấy danh sách mã giảm giá theo cấp độ thành viên (chỉ lấy mã đang hoạt động)
        Task<IEnumerable<MembershipDiscount>> GetMembershipDiscountsByLevelAsync(MembershipLevel level);
        
        // Phương thức lấy tất cả mã giảm giá thành viên
        Task<IEnumerable<MembershipDiscount>> GetAllMembershipDiscountsAsync();
        
        // Phương thức tạo mã giảm giá thành viên mới
        Task<int> CreateMembershipDiscountAsync(MembershipDiscount discount);
        
        // Phương thức cập nhật mã giảm giá thành viên
        Task UpdateMembershipDiscountAsync(MembershipDiscount discount);
        
        // Phương thức xóa mã giảm giá thành viên
        Task DeleteMembershipDiscountAsync(int id);
        
        // Phương thức lấy mã giảm giá thành viên theo ID
        Task<MembershipDiscount?> GetMembershipDiscountByIdAsync(int id);
        
        // Phương thức lấy mã giảm giá thành viên theo mã code
        Task<MembershipDiscount?> GetMembershipDiscountByCodeAsync(string code);
        
        // Phương thức kiểm tra mã giảm giá thành viên có hợp lệ không
        Task<bool> IsValidMembershipDiscountAsync(string code, MembershipLevel level);
    }
}