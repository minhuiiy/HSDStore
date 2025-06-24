using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CPTStore.Services
{
    public class DiscountService : IDiscountService
    {
        private readonly ApplicationDbContext _context;

        public DiscountService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Discount>> GetAllDiscountsAsync()
        {
            return await _context.Discounts
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<Discount?> GetDiscountByIdAsync(int id)
        {
            return await _context.Discounts.FindAsync(id);
        }

        public async Task<Discount?> GetDiscountByCodeAsync(string code)
        {
            return await _context.Discounts
                .FirstOrDefaultAsync(d => d.Code == code);
        }

        public async Task<int> CreateDiscountAsync(Discount discount)
        {
            _context.Discounts.Add(discount);
            await _context.SaveChangesAsync();
            return discount.Id;
        }

        public async Task UpdateDiscountAsync(Discount discount)
        {
            _context.Discounts.Update(discount);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteDiscountAsync(int id)
        {
            var discount = await _context.Discounts.FindAsync(id);
            if (discount != null)
            {
                _context.Discounts.Remove(discount);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsDiscountValidAsync(string code, decimal orderAmount)
        {
            var discount = await GetDiscountByCodeAsync(code);
            if (discount == null)
            {
                return false;
            }

            // Kiểm tra thời gian hiệu lực và trạng thái
            if (!discount.IsActive)
            {
                return false;
            }
            
            var now = DateTime.UtcNow;
            if (discount.StartDate > now || (discount.EndDate.HasValue && discount.EndDate.Value < now))
            {
                return false;
            }

            // Kiểm tra số lần sử dụng
            if (discount.UsageLimit.HasValue && discount.UsageCount >= discount.UsageLimit.Value)
            {
                return false;
            }

            // Kiểm tra giá trị đơn hàng tối thiểu
            if (discount.MinimumOrderAmount > 0 && orderAmount < discount.MinimumOrderAmount)
            {
                return false;
            }

            return true;
        }

        public async Task<decimal> CalculateDiscountAmountAsync(string code, decimal orderAmount)
        {
            var discount = await GetDiscountByCodeAsync(code);
            if (discount == null || !await IsDiscountValidAsync(code, orderAmount))
            {
                return 0;
            }

            decimal discountAmount = 0;

            if (discount.DiscountType == DiscountType.Percentage)
            {
                // Giảm giá theo phần trăm
                discountAmount = orderAmount * discount.Value / 100;

                // Giới hạn số tiền giảm tối đa (nếu có)
                if (discount.MaximumDiscountAmount.HasValue && discountAmount > discount.MaximumDiscountAmount.Value)
                {
                    discountAmount = discount.MaximumDiscountAmount.Value;
                }
            }
            else
            {
                // Giảm giá cố định
                discountAmount = discount.Value;

                // Không giảm nhiều hơn giá trị đơn hàng
                if (discountAmount > orderAmount)
                {
                    discountAmount = orderAmount;
                }
            }

            return discountAmount;
        }

        public async Task IncrementUsageCountAsync(int id)
        {
            var discount = await _context.Discounts.FindAsync(id);
            if (discount != null)
            {
                discount.UsageCount++;
                _context.Discounts.Update(discount);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Discount>> GetActiveDiscountsAsync()
        {
            var now = DateTime.UtcNow;
            return await _context.Discounts
                .Where(d => d.IsActive &&
                           d.StartDate <= now && 
                           (!d.EndDate.HasValue || d.EndDate.Value >= now) &&
                           (!d.UsageLimit.HasValue || d.UsageCount < d.UsageLimit.Value))
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }
        
        public async Task<IEnumerable<Discount>> GetDiscountsAsync(string? searchTerm, bool? isActive, int page, int pageSize)
        {
            var query = _context.Discounts.AsQueryable();
            
            // Áp dụng bộ lọc tìm kiếm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(d => d.Code.Contains(searchTerm) || 
                                         d.Description.Contains(searchTerm));
            }
            
            // Áp dụng bộ lọc trạng thái
            if (isActive.HasValue)
            {
                query = query.Where(d => d.IsActive == isActive.Value);
            }
            
            // Áp dụng phân trang
            return await query
                .OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        
        public async Task<int> GetTotalDiscountCountAsync(string? searchTerm, bool? isActive)
        {
            var query = _context.Discounts.AsQueryable();
            
            // Áp dụng bộ lọc tìm kiếm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(d => d.Code.Contains(searchTerm) || 
                                         d.Description.Contains(searchTerm));
            }
            
            // Áp dụng bộ lọc trạng thái
            if (isActive.HasValue)
            {
                query = query.Where(d => d.IsActive == isActive.Value);
            }
            
            return await query.CountAsync();
        }
    }
}