using CPTStore.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CPTStore.Services.Interfaces
{
    public interface ISavedDiscountService
    {
        /// <summary>
        /// Lấy tất cả mã giảm giá đã lưu của người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Danh sách mã giảm giá đã lưu</returns>
        Task<IEnumerable<SavedDiscount>> GetSavedDiscountsByUserIdAsync(string userId);

        /// <summary>
        /// Lấy mã giảm giá đã lưu theo ID
        /// </summary>
        /// <param name="id">ID của mã giảm giá đã lưu</param>
        /// <returns>Mã giảm giá đã lưu</returns>
        Task<SavedDiscount?> GetSavedDiscountByIdAsync(int id);

        /// <summary>
        /// Kiểm tra xem người dùng đã lưu mã giảm giá này chưa
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="discountCode">Mã giảm giá</param>
        /// <returns>True nếu đã lưu, False nếu chưa lưu</returns>
        Task<bool> HasUserSavedDiscountAsync(string userId, string discountCode);

        /// <summary>
        /// Lưu mã giảm giá cho người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="discountCode">Mã giảm giá</param>
        /// <returns>ID của mã giảm giá đã lưu</returns>
        Task<int> SaveDiscountAsync(string userId, string discountCode);

        /// <summary>
        /// Đánh dấu mã giảm giá đã được sử dụng
        /// </summary>
        /// <param name="id">ID của mã giảm giá đã lưu</param>
        /// <returns>Task</returns>
        Task MarkAsUsedAsync(int id);

        /// <summary>
        /// Đánh dấu mã giảm giá đã được sử dụng theo mã code và userId
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="discountCode">Mã giảm giá</param>
        /// <returns>Task</returns>
        Task MarkAsUsedAsync(string userId, string discountCode);

        /// <summary>
        /// Xóa mã giảm giá đã lưu
        /// </summary>
        /// <param name="id">ID của mã giảm giá đã lưu</param>
        /// <returns>Task</returns>
        Task DeleteSavedDiscountAsync(int id);
    }
}