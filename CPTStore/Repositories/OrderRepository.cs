using CPTStore.Data;
using CPTStore.Extensions;
using CPTStore.Models;
using CPTStore.Repositories.Interfaces;
using CPTStore.ViewModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CPTStore.Repositories
{
    /// <summary>
    /// Triển khai các phương thức truy cập dữ liệu cho Order
    /// </summary>
    public class OrderRepository : IOrderRepository
    {
        private readonly ApplicationDbContext _context;

        public OrderRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy một Order theo ID
        /// </summary>
        /// <param name="id">ID của Order</param>
        /// <returns>Order nếu tìm thấy, null nếu không tìm thấy</returns>
        public async Task<Order?> GetByIdAsync(int id)
        {
            return await _context.Orders
                .IncludeStandardReferences()
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        /// <summary>
        /// Lấy một Order theo số đơn hàng
        /// </summary>
        /// <param name="orderNumber">Số đơn hàng</param>
        /// <returns>Order nếu tìm thấy, null nếu không tìm thấy</returns>
        public async Task<Order?> GetByOrderNumberAsync(string orderNumber)
        {
            return await _context.Orders
                .IncludeStandardReferences()
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
        }

        /// <summary>
        /// Lấy tất cả các Order của một người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Danh sách Order</returns>
        public async Task<IEnumerable<Order>> GetByUserIdAsync(string userId)
        {
            return await _context.Orders
                .IncludeStandardReferences()
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy tất cả các Order
        /// </summary>
        /// <param name="status">Trạng thái đơn hàng (tùy chọn)</param>
        /// <returns>Danh sách Order</returns>
        public async Task<IEnumerable<Order>> GetAllAsync(OrderStatus? status = null)
        {
            var query = _context.Orders.IncludeStandardReferences();

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            return await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        }

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
        public async Task<IEnumerable<Order>> GetOrdersAsync(string? searchTerm, string? status, DateTime? fromDate, DateTime? toDate, int page, int pageSize)
        {
            var query = _context.Orders.IncludeStandardReferences();

            // Áp dụng các điều kiện lọc
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(o =>
                    o.OrderNumber.Contains(searchTerm) ||
                    o.CustomerName.Contains(searchTerm) ||
                    o.PhoneNumber.Contains(searchTerm) ||
                    o.Address.Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, out var orderStatus))
            {
                query = query.Where(o => o.Status == orderStatus);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt <= toDate.Value.AddDays(1));
            }

            // Phân trang
            return await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy các Order trong một khoảng thời gian
        /// </summary>
        /// <param name="startDate">Ngày bắt đầu</param>
        /// <param name="endDate">Ngày kết thúc</param>
        /// <returns>Danh sách Order</returns>
        public async Task<IEnumerable<Order>> GetOrdersInPeriodAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Orders
                .IncludeStandardReferences()
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy các Order gần đây
        /// </summary>
        /// <param name="count">Số lượng Order cần lấy</param>
        /// <returns>Danh sách Order</returns>
        public async Task<IEnumerable<Order>> GetRecentOrdersAsync(int count)
        {
            return await _context.Orders
                .IncludeStandardReferences()
                .OrderByDescending(o => o.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy tổng số Order
        /// </summary>
        /// <returns>Tổng số Order</returns>
        public async Task<int> GetTotalCountAsync()
        {
            return await _context.Orders.CountAsync();
        }

        /// <summary>
        /// Lấy tổng số Order theo điều kiện tìm kiếm
        /// </summary>
        /// <param name="searchTerm">Từ khóa tìm kiếm</param>
        /// <param name="status">Trạng thái đơn hàng</param>
        /// <param name="fromDate">Ngày bắt đầu</param>
        /// <param name="toDate">Ngày kết thúc</param>
        /// <returns>Tổng số Order</returns>
        public async Task<int> GetTotalCountAsync(string? searchTerm, string? status, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.Orders.AsQueryable();

            // Áp dụng các điều kiện lọc
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(o =>
                    o.OrderNumber.Contains(searchTerm) ||
                    o.CustomerName.Contains(searchTerm) ||
                    o.PhoneNumber.Contains(searchTerm) ||
                    o.Address.Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, out var orderStatus))
            {
                query = query.Where(o => o.Status == orderStatus);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt <= toDate.Value.AddDays(1));
            }

            return await query.CountAsync();
        }

        /// <summary>
        /// Lấy thông tin tóm tắt của các Order
        /// </summary>
        /// <param name="status">Trạng thái đơn hàng (tùy chọn)</param>
        /// <returns>Danh sách OrderSummaryDto</returns>
        public async Task<IEnumerable<OrderSummaryDto>> GetOrderSummariesAsync(OrderStatus? status = null)
        {
            var query = _context.Orders.AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            return await query
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderSummaryDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    CustomerName = o.CustomerName,
                    CreatedAt = o.CreatedAt,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    PaymentStatus = o.PaymentStatus,
                    ItemCount = o.OrderItems.Count()
                })
                .ToListAsync();
        }

        /// <summary>
        /// Thêm một Order mới
        /// </summary>
        /// <param name="order">Order cần thêm</param>
        /// <returns>Order đã được thêm</returns>
        public async Task<Order> AddAsync(Order order)
        {
            await _context.Orders.AddAsync(order);
            await _context.SaveChangesAsync();
            return order;
        }

        /// <summary>
        /// Cập nhật một Order
        /// </summary>
        /// <param name="order">Order cần cập nhật</param>
        /// <returns>Task</returns>
        public async Task UpdateAsync(Order order)
        {
            // Tìm đơn hàng hiện có trong database
            var existingOrder = await _context.Orders.FindAsync(order.Id);
            if (existingOrder == null)
            {
                throw new InvalidOperationException($"Không tìm thấy đơn hàng với ID: {order.Id}");
            }

            // Cập nhật các thuộc tính của đơn hàng hiện có
            _context.Entry(existingOrder).CurrentValues.SetValues(order);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Xóa một Order
        /// </summary>
        /// <param name="id">ID của Order cần xóa</param>
        /// <returns>Task</returns>
        public async Task DeleteAsync(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order != null)
            {
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
            }
        }
    }
}