using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CPTStore.Repositories
{
    /// <summary>
    /// Triển khai các phương thức thao tác với CartItem
    /// </summary>
    public class CartItemRepository : ICartItemRepository
    {
        private readonly ApplicationDbContext _context;

        public CartItemRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy tất cả các CartItem của một người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Danh sách CartItem</returns>
        public async Task<List<CartItem>> GetCartItemsByUserIdAsync(string userId)
        {
            return await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.UserId == userId)
                .ToListAsync();
        }

        /// <summary>
    /// Lấy một CartItem theo ID
    /// </summary>
    /// <param name="id">ID của CartItem</param>
    /// <returns>CartItem nếu tìm thấy, null nếu không tìm thấy</returns>
    public async Task<CartItem?> GetCartItemByIdAsync(int id)
    {
        return await _context.CartItems
            .Include(c => c.Product)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

        /// <summary>
    /// Lấy một CartItem theo ProductId và UserId
    /// </summary>
    /// <param name="productId">ID của sản phẩm</param>
    /// <param name="userId">ID của người dùng</param>
    /// <returns>CartItem nếu tìm thấy, null nếu không tìm thấy</returns>
    public async Task<CartItem?> GetCartItemByProductIdAndUserIdAsync(int productId, string userId)
    {
        return await _context.CartItems
            .Include(c => c.Product)
            .FirstOrDefaultAsync(c => c.ProductId == productId && c.UserId == userId);
    }

        /// <summary>
        /// Thêm một CartItem mới
        /// </summary>
        /// <param name="cartItem">CartItem cần thêm</param>
        /// <returns>CartItem đã được thêm</returns>
        public async Task<CartItem> AddCartItemAsync(CartItem cartItem)
        {
            cartItem.CreatedAt = DateTime.Now;
            cartItem.UpdatedAt = DateTime.Now;
            
            _context.CartItems.Add(cartItem);
            await _context.SaveChangesAsync();
            
            return cartItem;
        }

        /// <summary>
        /// Cập nhật một CartItem
        /// </summary>
        /// <param name="cartItem">CartItem cần cập nhật</param>
        /// <returns>CartItem đã được cập nhật</returns>
        public async Task<CartItem> UpdateCartItemAsync(CartItem cartItem)
        {
            cartItem.UpdatedAt = DateTime.Now;
            
            _context.CartItems.Update(cartItem);
            await _context.SaveChangesAsync();
            
            return cartItem;
        }

        /// <summary>
        /// Xóa một CartItem
        /// </summary>
        /// <param name="cartItem">CartItem cần xóa</param>
        /// <returns>True nếu xóa thành công, False nếu xóa thất bại</returns>
        public async Task<bool> DeleteCartItemAsync(CartItem cartItem)
        {
            try
            {
                if (cartItem == null)
                {
                    throw new ArgumentNullException(nameof(cartItem), "CartItem không thể null");
                }
                
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex)
            {
                // Ghi log lỗi cụ thể khi cập nhật database
                Console.WriteLine($"Lỗi khi xóa CartItem: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                // Ghi log lỗi chung
                Console.WriteLine($"Lỗi không xác định khi xóa CartItem: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Xóa tất cả CartItem của một người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>True nếu xóa thành công, False nếu xóa thất bại</returns>
        public async Task<bool> ClearCartAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentException("UserId không thể null hoặc rỗng", nameof(userId));
                }
                
                var cartItems = await _context.CartItems
                    .Where(c => c.UserId == userId)
                    .ToListAsync();
                
                if (cartItems.Any())
                {
                    _context.CartItems.RemoveRange(cartItems);
                    await _context.SaveChangesAsync();
                }
                
                return true;
            }
            catch (DbUpdateException ex)
            {
                // Ghi log lỗi cụ thể khi cập nhật database
                Console.WriteLine($"Lỗi khi xóa tất cả CartItem: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                // Ghi log lỗi chung
                Console.WriteLine($"Lỗi không xác định khi xóa tất cả CartItem: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tính tổng tiền của tất cả CartItem của một người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Tổng tiền</returns>
        public async Task<decimal> GetCartTotalAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentException("UserId không thể null hoặc rỗng", nameof(userId));
                }
                
                // Tối ưu hóa truy vấn bằng cách chỉ chọn các trường cần thiết
                return await _context.CartItems
                    .Where(c => c.UserId == userId)
                    .Join(_context.Products,
                        cartItem => cartItem.ProductId,
                        product => product.Id,
                        (cartItem, product) => new { cartItem.Quantity, product.Price })
                    .SumAsync(x => x.Price * x.Quantity);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tính tổng giỏ hàng: {ex.Message}");
                return 0; // Trả về 0 nếu có lỗi
            }
        }
    }
}