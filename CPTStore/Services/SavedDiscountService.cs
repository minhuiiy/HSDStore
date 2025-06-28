using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CPTStore.Services
{
    public class SavedDiscountService : ISavedDiscountService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDiscountService _discountService;
        private readonly ILogger<SavedDiscountService> _logger;

        public SavedDiscountService(ApplicationDbContext context, IDiscountService discountService, ILogger<SavedDiscountService> logger)
        {
            _context = context;
            _discountService = discountService;
            _logger = logger;
        }

        public async Task<IEnumerable<SavedDiscount>> GetSavedDiscountsByUserIdAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentException("UserId không được để trống");
                }

                return await _context.SavedDiscounts
                    .Include(sd => sd.Discount)
                    .Where(sd => sd.UserId == userId)
                    .OrderByDescending(sd => sd.SavedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách mã giảm giá đã lưu cho userId {UserId}: {Message}", userId, ex.Message);
                throw;
            }
        }

        public async Task<SavedDiscount?> GetSavedDiscountByIdAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    throw new ArgumentException("ID không hợp lệ");
                }

                return await _context.SavedDiscounts
                    .Include(sd => sd.Discount)
                    .FirstOrDefaultAsync(sd => sd.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin mã giảm giá đã lưu với ID {Id}: {Message}", id, ex.Message);
                throw;
            }
        }

        public async Task<bool> HasUserSavedDiscountAsync(string userId, string discountCode)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentException("UserId không được để trống");
                }

                if (string.IsNullOrEmpty(discountCode))
                {
                    throw new ArgumentException("DiscountCode không được để trống");
                }

                return await _context.SavedDiscounts
                    .AnyAsync(sd => sd.UserId == userId && sd.DiscountCode == discountCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra mã giảm giá đã lưu cho userId {UserId} và mã {DiscountCode}: {Message}", userId, discountCode, ex.Message);
                throw;
            }
        }

        public async Task<int> SaveDiscountAsync(string userId, string discountCode)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentException("UserId không được để trống");
                }

                if (string.IsNullOrEmpty(discountCode))
                {
                    throw new ArgumentException("DiscountCode không được để trống");
                }

                // Kiểm tra xem mã giảm giá có tồn tại không
                var discount = await _discountService.GetDiscountByCodeAsync(discountCode);
                if (discount == null)
                {
                    throw new ArgumentException("Mã giảm giá không tồn tại");
                }

                // Kiểm tra xem người dùng đã lưu mã giảm giá này chưa
                var existingSavedDiscount = await _context.SavedDiscounts
                    .FirstOrDefaultAsync(sd => sd.UserId == userId && sd.DiscountCode == discountCode);

                if (existingSavedDiscount != null)
                {
                    throw new InvalidOperationException("Bạn đã lưu mã giảm giá này rồi");
                }

                // Kiểm tra xem người dùng có tồn tại không
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    throw new ArgumentException("Người dùng không tồn tại");
                }

                var savedDiscount = new SavedDiscount
                {
                    UserId = userId,
                    DiscountId = discount.Id,
                    DiscountCode = discountCode,
                    SavedDate = DateTime.UtcNow,
                    IsUsed = false
                };

                _context.SavedDiscounts.Add(savedDiscount);
                await _context.SaveChangesAsync();

                return savedDiscount.Id;
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Lỗi cơ sở dữ liệu khi lưu mã giảm giá cho userId {UserId} và mã {DiscountCode}: {Message}", userId, discountCode, dbEx.Message);
                throw new InvalidOperationException("Không thể lưu mã giảm giá do lỗi cơ sở dữ liệu. Vui lòng thử lại sau.", dbEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu mã giảm giá cho userId {UserId} và mã {DiscountCode}: {Message}", userId, discountCode, ex.Message);
                throw;
            }
        }

        public async Task MarkAsUsedAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    throw new ArgumentException("ID không hợp lệ");
                }

                var savedDiscount = await _context.SavedDiscounts.FindAsync(id);
                if (savedDiscount == null)
                {
                    throw new ArgumentException("Mã giảm giá đã lưu không tồn tại");
                }

                if (savedDiscount.IsUsed)
                {
                    throw new InvalidOperationException("Mã giảm giá này đã được sử dụng");
                }

                savedDiscount.IsUsed = true;
                savedDiscount.UsedDate = DateTime.UtcNow;

                _context.SavedDiscounts.Update(savedDiscount);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Lỗi cơ sở dữ liệu khi đánh dấu mã giảm giá đã sử dụng với ID {Id}: {Message}", id, dbEx.Message);
                throw new InvalidOperationException("Không thể đánh dấu mã giảm giá đã sử dụng do lỗi cơ sở dữ liệu. Vui lòng thử lại sau.", dbEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đánh dấu mã giảm giá đã sử dụng với ID {Id}: {Message}", id, ex.Message);
                throw;
            }
        }

        public async Task MarkAsUsedAsync(string userId, string discountCode)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentException("UserId không được để trống");
                }

                if (string.IsNullOrEmpty(discountCode))
                {
                    throw new ArgumentException("DiscountCode không được để trống");
                }

                var savedDiscount = await _context.SavedDiscounts
                    .FirstOrDefaultAsync(sd => sd.UserId == userId && sd.DiscountCode == discountCode);

                if (savedDiscount == null)
                {
                    throw new ArgumentException("Mã giảm giá đã lưu không tồn tại");
                }

                if (savedDiscount.IsUsed)
                {
                    throw new InvalidOperationException("Mã giảm giá này đã được sử dụng");
                }

                savedDiscount.IsUsed = true;
                savedDiscount.UsedDate = DateTime.UtcNow;

                _context.SavedDiscounts.Update(savedDiscount);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Lỗi cơ sở dữ liệu khi đánh dấu mã giảm giá đã sử dụng cho userId {UserId} và mã {DiscountCode}: {Message}", userId, discountCode, dbEx.Message);
                throw new InvalidOperationException("Không thể đánh dấu mã giảm giá đã sử dụng do lỗi cơ sở dữ liệu. Vui lòng thử lại sau.", dbEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đánh dấu mã giảm giá đã sử dụng cho userId {UserId} và mã {DiscountCode}: {Message}", userId, discountCode, ex.Message);
                throw;
            }
        }

        public async Task DeleteSavedDiscountAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    throw new ArgumentException("ID không hợp lệ");
                }

                var savedDiscount = await _context.SavedDiscounts.FindAsync(id);
                if (savedDiscount == null)
                {
                    throw new ArgumentException("Mã giảm giá đã lưu không tồn tại");
                }

                _context.SavedDiscounts.Remove(savedDiscount);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Lỗi cơ sở dữ liệu khi xóa mã giảm giá đã lưu với ID {Id}: {Message}", id, dbEx.Message);
                throw new InvalidOperationException("Không thể xóa mã giảm giá đã lưu do lỗi cơ sở dữ liệu. Vui lòng thử lại sau.", dbEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa mã giảm giá đã lưu với ID {Id}: {Message}", id, ex.Message);
                throw;
            }
        }
    }
}