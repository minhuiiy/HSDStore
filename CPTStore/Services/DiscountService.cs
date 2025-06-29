using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CPTStore.Services
{
    public class DiscountService(ApplicationDbContext _context) : IDiscountService
    {

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
            try
            {
                if (discount is null)
                {
                    throw new ArgumentNullException(nameof(discount), "Discount không được để trống");
                }

                // Đảm bảo CreatedAt luôn là DateTime.UtcNow
                discount.CreatedAt = DateTime.UtcNow;
                discount.UpdatedAt = null;

                _context.Discounts.Add(discount);
                await _context.SaveChangesAsync();
                return discount.Id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tạo mã giảm giá: {ex.Message}");
                if (ex.InnerException is not null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task UpdateDiscountAsync(Discount discount)
        {
            try
            {
                if (discount is null)
                {
                    throw new ArgumentNullException(nameof(discount), "Discount không được để trống");
                }

                discount.UpdatedAt = DateTime.UtcNow;
                
                // Kiểm tra xem entity đã được theo dõi chưa
                var existingDiscount = await _context.Discounts.FindAsync(discount.Id);
                if (existingDiscount != null)
                {
                    // Cập nhật các thuộc tính của entity đã được theo dõi
                    _context.Entry(existingDiscount).CurrentValues.SetValues(discount);
                }
                else
                {
                    // Nếu entity chưa được theo dõi, thêm vào context
                    _context.Discounts.Update(discount);
                }
                
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi cập nhật mã giảm giá: {ex.Message}");
                if (ex.InnerException is not null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task DeleteDiscountAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    throw new ArgumentException("ID không hợp lệ");
                }

                var discount = await _context.Discounts.FindAsync(id);
                if (discount is null)
                {
                    throw new ArgumentException("Mã giảm giá không tồn tại");
                }

                // Kiểm tra xem mã giảm giá có đang được sử dụng không
                var savedDiscounts = await _context.SavedDiscounts
                    .Where(sd => sd.DiscountId == id)
                    .ToListAsync();

                if (savedDiscounts.Count > 0)
                {
                    // Xóa tất cả các mã giảm giá đã lưu liên quan
                    _context.SavedDiscounts.RemoveRange(savedDiscounts);
                }

                _context.Discounts.Remove(discount);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xóa mã giảm giá: {ex.Message}");
                if (ex.InnerException is not null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
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
            if (discount is null || !await IsDiscountValidAsync(code, orderAmount))
            {
                return 0;
            }

            decimal discountAmount;

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
            try
            {
                if (id <= 0)
                {
                    throw new ArgumentException("ID không hợp lệ");
                }

                var discount = await _context.Discounts.FindAsync(id);
                if (discount is null)
                {
                    throw new ArgumentException("Mã giảm giá không tồn tại");
                }

                discount.UsageCount++;
                discount.UpdatedAt = DateTime.UtcNow;
                _context.Discounts.Update(discount);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tăng số lần sử dụng mã giảm giá: {ex.Message}");
                if (ex.InnerException is not null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
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