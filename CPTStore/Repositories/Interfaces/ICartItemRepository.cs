using CPTStore.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CPTStore.Repositories.Interfaces
{
    /// <summary>
    /// Interface định nghĩa các phương thức thao tác với CartItem
    /// </summary>
    public interface ICartItemRepository
    {
        /// <summary>
        /// Lấy tất cả các CartItem của một người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Danh sách CartItem</returns>
        Task<List<CartItem>> GetCartItemsByUserIdAsync(string userId);
        
        /// <summary>
        /// Lấy một CartItem theo ID
        /// </summary>
        /// <param name="id">ID của CartItem</param>
        /// <returns>CartItem nếu tìm thấy, null nếu không tìm thấy</returns>
        Task<CartItem?> GetCartItemByIdAsync(int id);
        
        /// <summary>
        /// Lấy một CartItem theo ProductId và UserId
        /// </summary>
        /// <param name="productId">ID của sản phẩm</param>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>CartItem nếu tìm thấy, null nếu không tìm thấy</returns>
        Task<CartItem?> GetCartItemByProductIdAndUserIdAsync(int productId, string userId);
        
        /// <summary>
        /// Thêm một CartItem mới
        /// </summary>
        /// <param name="cartItem">CartItem cần thêm</param>
        /// <returns>CartItem đã được thêm</returns>
        Task<CartItem> AddCartItemAsync(CartItem cartItem);
        
        /// <summary>
        /// Cập nhật một CartItem
        /// </summary>
        /// <param name="cartItem">CartItem cần cập nhật</param>
        /// <returns>CartItem đã được cập nhật</returns>
        Task<CartItem> UpdateCartItemAsync(CartItem cartItem);
        
        /// <summary>
        /// Xóa một CartItem
        /// </summary>
        /// <param name="cartItem">CartItem cần xóa</param>
        /// <returns>True nếu xóa thành công, False nếu xóa thất bại</returns>
        Task<bool> DeleteCartItemAsync(CartItem cartItem);
        
        /// <summary>
        /// Xóa tất cả CartItem của một người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>True nếu xóa thành công, False nếu xóa thất bại</returns>
        Task<bool> ClearCartAsync(string userId);
        
        /// <summary>
        /// Tính tổng tiền của tất cả CartItem của một người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Tổng tiền</returns>
        Task<decimal> GetCartTotalAsync(string userId);
    }
}