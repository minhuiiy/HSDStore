using CPTStore.Models;
using CPTStore.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CPTStore.Services.Interfaces
{
    /// <summary>
    /// Interface định nghĩa các phương thức xử lý giỏ hàng
    /// </summary>
    public interface ICartService
    {
        /// <summary>
        /// Lấy tất cả các CartItem của một người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Danh sách CartItemViewModel</returns>
        Task<List<CartItemViewModel>> GetCartItemsAsync(string userId);

        /// <summary>
        /// Lấy một CartItem theo ID
        /// </summary>
        /// <param name="id">ID của CartItem</param>
        /// <returns>CartItemViewModel nếu tìm thấy, null nếu không tìm thấy</returns>
        Task<CartItemViewModel?> GetCartItemAsync(int id);

        /// <summary>
        /// Lấy một CartItem theo ProductId và UserId
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="productId">ID của sản phẩm</param>
        /// <returns>CartItemViewModel nếu tìm thấy, null nếu không tìm thấy</returns>
        Task<CartItemViewModel?> GetCartItemAsync(string userId, int productId);

        /// <summary>
        /// Thêm sản phẩm vào giỏ hàng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="productId">ID của sản phẩm</param>
        /// <param name="quantity">Số lượng sản phẩm</param>
        /// <returns>ID của CartItem nếu thành công, -1 nếu thất bại</returns>
        Task<int> AddToCartAsync(string userId, int productId, int quantity);

        /// <summary>
        /// Cập nhật số lượng sản phẩm trong giỏ hàng
        /// </summary>
        /// <param name="id">ID của CartItem</param>
        /// <param name="quantity">Số lượng mới</param>
        /// <returns>Task</returns>
        Task UpdateCartItemAsync(int id, int quantity);

        /// <summary>
        /// Xóa sản phẩm khỏi giỏ hàng
        /// </summary>
        /// <param name="id">ID của CartItem</param>
        /// <returns>Task</returns>
        Task RemoveFromCartAsync(int id);

        /// <summary>
        /// Xóa tất cả sản phẩm trong giỏ hàng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Task</returns>
        Task ClearCartAsync(string userId);

        /// <summary>
        /// Tính tổng số lượng sản phẩm trong giỏ hàng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Tổng số lượng</returns>
        Task<int> GetCartItemsCountAsync(string userId);

        /// <summary>
        /// Tính tổng tiền giỏ hàng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Tổng tiền</returns>
        Task<decimal> GetCartTotalAsync(string userId);

        /// <summary>
        /// Áp dụng mã giảm giá
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="discountCode">Mã giảm giá</param>
        /// <returns>Số tiền được giảm</returns>
        Task<decimal> ApplyDiscountAsync(string userId, string discountCode);

        /// <summary>
        /// Xóa mã giảm giá
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Task</returns>
        Task RemoveDiscountAsync(string userId);
    }
}