using CPTStore.Models;
using CPTStore.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CPTStore.Repositories.Interfaces
{
    /// <summary>
    /// Interface định nghĩa các phương thức truy cập dữ liệu cho Order
    /// </summary>
    public interface IOrderRepository
    {
        /// <summary>
        /// Lấy một Order theo ID
        /// </summary>
        /// <param name="id">ID của Order</param>
        /// <returns>Order nếu tìm thấy, null nếu không tìm thấy</returns>
        Task<Order?> GetByIdAsync(int id);

        /// <summary>
        /// Lấy một Order theo số đơn hàng
        /// </summary>
        /// <param name="orderNumber">Số đơn hàng</param>
        /// <returns>Order nếu tìm thấy, null nếu không tìm thấy</returns>
        Task<Order?> GetByOrderNumberAsync(string orderNumber);

        /// <summary>
        /// Lấy tất cả các Order của một người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Danh sách Order</returns>
        Task<IEnumerable<Order>> GetByUserIdAsync(string userId);

        /// <summary>
        /// Lấy tất cả các Order
        /// </summary>
        /// <param name="status">Trạng thái đơn hàng (tùy chọn)</param>
        /// <returns>Danh sách Order</returns>
        Task<IEnumerable<Order>> GetAllAsync(OrderStatus? status = null);

        /// <summary>
        /// Lấy các Order theo điều kiện tìm kiếm
        /// </summary>
        /// <param name="searchTerm">Từ khóa tìm kiếm</param>
        /// <param name="status">Trạng thái đơn hàng</param>
        /// <param name="fromDate">Ngày bắt đầu</param>
        /// <param name="toDate">Ngày kết thúc</param>
        /// <param name="page">Trang hiện tại</param>
        /// <param name="pageSize">Số lượng mục trên mỗi trang</param>
        /// <returns>Danh sách Order</returns>
        Task<IEnumerable<Order>> GetOrdersAsync(string? searchTerm, string? status, DateTime? fromDate, DateTime? toDate, int page, int pageSize);

        /// <summary>
        /// Lấy các Order trong một khoảng thời gian
        /// </summary>
        /// <param name="startDate">Ngày bắt đầu</param>
        /// <param name="endDate">Ngày kết thúc</param>
        /// <returns>Danh sách Order</returns>
        Task<IEnumerable<Order>> GetOrdersInPeriodAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Lấy các Order gần đây
        /// </summary>
        /// <param name="count">Số lượng Order cần lấy</param>
        /// <returns>Danh sách Order</returns>
        Task<IEnumerable<Order>> GetRecentOrdersAsync(int count);

        /// <summary>
        /// Lấy tổng số Order
        /// </summary>
        /// <returns>Tổng số Order</returns>
        Task<int> GetTotalCountAsync();

        /// <summary>
        /// Lấy tổng số Order theo điều kiện tìm kiếm
        /// </summary>
        /// <param name="searchTerm">Từ khóa tìm kiếm</param>
        /// <param name="status">Trạng thái đơn hàng</param>
        /// <param name="fromDate">Ngày bắt đầu</param>
        /// <param name="toDate">Ngày kết thúc</param>
        /// <returns>Tổng số Order</returns>
        Task<int> GetTotalCountAsync(string? searchTerm, string? status, DateTime? fromDate, DateTime? toDate);

        /// <summary>
        /// Lấy thông tin tóm tắt của các Order
        /// </summary>
        /// <param name="status">Trạng thái đơn hàng (tùy chọn)</param>
        /// <returns>Danh sách OrderSummaryDto</returns>
        Task<IEnumerable<OrderSummaryDto>> GetOrderSummariesAsync(OrderStatus? status = null);

        /// <summary>
        /// Thêm một Order mới
        /// </summary>
        /// <param name="order">Order cần thêm</param>
        /// <returns>Order đã được thêm</returns>
        Task<Order> AddAsync(Order order);

        /// <summary>
        /// Cập nhật một Order
        /// </summary>
        /// <param name="order">Order cần cập nhật</param>
        /// <returns>Task</returns>
        Task UpdateAsync(Order order);

        /// <summary>
        /// Xóa một Order
        /// </summary>
        /// <param name="id">ID của Order cần xóa</param>
        /// <returns>Task</returns>
        Task DeleteAsync(int id);
    }
}