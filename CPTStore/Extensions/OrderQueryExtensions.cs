using CPTStore.Models;
using Microsoft.EntityFrameworkCore;

namespace CPTStore.Extensions
{
    /// <summary>
    /// Các phương thức mở rộng cho truy vấn Order
    /// </summary>
    public static class OrderQueryExtensions
    {
        /// <summary>
        /// Bao gồm các tham chiếu tiêu chuẩn cho truy vấn Order
        /// </summary>
        /// <param name="query">Truy vấn Order</param>
        /// <returns>Truy vấn với các tham chiếu đã được bao gồm</returns>
        public static IQueryable<Order> IncludeStandardReferences(this IQueryable<Order> query)
        {
            return query
                .Include(o => o.OrderItems)
                .Include(o => o.ApplicationUser);
        }

        /// <summary>
        /// Bao gồm chỉ các tham chiếu cần thiết cho xử lý đơn hàng
        /// </summary>
        /// <param name="query">Truy vấn Order</param>
        /// <returns>Truy vấn với các tham chiếu cần thiết cho xử lý</returns>
        public static IQueryable<Order> IncludeProcessingReferences(this IQueryable<Order> query)
        {
            return query
                .Include(o => o.OrderItems);
        }
    }
}