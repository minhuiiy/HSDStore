using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CPTStore.Services
{
    public class MembershipService : IMembershipService
    {
        private readonly ApplicationDbContext _context;

        public MembershipService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task UpdateMembershipLevelAsync(string userId, decimal orderAmount)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("Người dùng không tồn tại");
            }

            // Cập nhật tổng giá trị đơn hàng
            user.TotalPurchases += orderAmount;

            // Cập nhật cấp độ thành viên dựa trên tổng giá trị đơn hàng
            if (user.TotalPurchases >= 10000000)
            {
                user.MembershipLevel = MembershipLevel.Diamond;
            }
            else if (user.TotalPurchases >= 5000000)
            {
                user.MembershipLevel = MembershipLevel.Gold;
            }
            else if (user.TotalPurchases >= 2000000)
            {
                user.MembershipLevel = MembershipLevel.Silver;
            }
            else
            {
                user.MembershipLevel = MembershipLevel.Regular;
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task<MembershipLevel> GetMembershipLevelAsync(string userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("Người dùng không tồn tại");
            }

            return user.MembershipLevel;
        }

        public async Task<decimal> GetTotalPurchasesAsync(string userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("Người dùng không tồn tại");
            }

            return user.TotalPurchases;
        }

        public async Task<IEnumerable<MembershipDiscount>> GetMembershipDiscountsAsync(MembershipLevel level)
        {
            return await _context.MembershipDiscounts
                .Where(md => md.MembershipLevel == level && md.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<MembershipDiscount>> GetAllMembershipDiscountsAsync()
        {
            return await _context.MembershipDiscounts
                .OrderBy(md => md.MembershipLevel)
                .ThenByDescending(md => md.Value)
                .ToListAsync();
        }

        public async Task<int> CreateMembershipDiscountAsync(MembershipDiscount discount)
        {
            _context.MembershipDiscounts.Add(discount);
            await _context.SaveChangesAsync();
            return discount.Id;
        }

        public async Task UpdateMembershipDiscountAsync(MembershipDiscount discount)
        {
            _context.MembershipDiscounts.Update(discount);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteMembershipDiscountAsync(int id)
        {
            var discount = await _context.MembershipDiscounts.FindAsync(id);
            if (discount != null)
            {
                _context.MembershipDiscounts.Remove(discount);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<MembershipDiscount?> GetMembershipDiscountByIdAsync(int id)
        {
            return await _context.MembershipDiscounts.FindAsync(id);
        }

        public async Task<MembershipDiscount?> GetMembershipDiscountByCodeAsync(string code)
        {
            return await _context.MembershipDiscounts
                .FirstOrDefaultAsync(md => md.Code == code);
        }

        public async Task<bool> IsValidMembershipDiscountAsync(string code, MembershipLevel level)
        {
            var discount = await _context.MembershipDiscounts
                .FirstOrDefaultAsync(md => md.Code == code && md.IsActive);

            if (discount == null)
            {
                return false;
            }

            // Kiểm tra xem mã giảm giá có áp dụng cho cấp độ thành viên hiện tại không
            // Lưu ý: Người dùng có thể sử dụng mã giảm giá của cấp độ thành viên thấp hơn
            return discount.MembershipLevel <= level;
        }

        public async Task<IEnumerable<MembershipDiscount>> GetMembershipDiscountsByLevelAsync(MembershipLevel level)
        {
            // Lấy tất cả mã giảm giá áp dụng cho cấp độ thành viên hiện tại và thấp hơn
            return await _context.MembershipDiscounts
                .Where(md => md.MembershipLevel <= level && md.IsActive)
                .OrderBy(md => md.MembershipLevel)
                .ThenByDescending(md => md.Value)
                .ToListAsync();
        }
    }
}