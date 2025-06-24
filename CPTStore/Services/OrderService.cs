using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using CPTStore.Areas.Admin.ViewModels;
using CPTStore.Extensions;
using CPTStore.ViewModels;

namespace CPTStore.Services
{
    public class OrderService(
        ApplicationDbContext context,
        ICartService cartService,
        IInventoryService inventoryService,
        IPdfService pdfService,
        IEmailService emailService) : IOrderService
    {
        private readonly ApplicationDbContext _context = context;
        private readonly ICartService _cartService = cartService;
        private readonly IInventoryService _inventoryService = inventoryService;
        private readonly IPdfService _pdfService = pdfService;
        private readonly IEmailService _emailService = emailService;

        public async Task<Order> CreateOrderAsync(
            string userId, 
            string customerName, 
            string phoneNumber, 
            string address, 
            string? city, 
            string? postalCode, 
            PaymentMethod paymentMethod, 
            string? notes)
        {
            // Lấy giỏ hàng của người dùng
            var cartItems = await _cartService.GetCartItemsAsync(userId);
            if (cartItems.Count == 0)
            {
                throw new InvalidOperationException("Giỏ hàng trống");
            }

            // Kiểm tra tồn kho
            foreach (var item in cartItems)
            {
                if (!await _inventoryService.IsInStockAsync(item.ProductId, item.Quantity))
                {
                    throw new InvalidOperationException($"Sản phẩm '{item.Product?.Name ?? "Không xác định"}' không đủ số lượng trong kho");
                }
            }

            // Tính tổng tiền
            decimal subtotal = await _cartService.GetCartTotalAsync(userId);
            
            // Lấy thông tin giảm giá (nếu có)
            var cartDiscount = await _context.CartDiscounts
                .FirstOrDefaultAsync(cd => cd.UserId == userId);
            
            decimal discountAmount = cartDiscount?.DiscountAmount ?? 0;
            string? discountCode = cartDiscount?.DiscountCode;

            // Tạo mã đơn hàng
            string orderNumber = GenerateOrderNumber();

            // Tạo đơn hàng mới
            var order = new Order
            {
                UserId = userId,
                OrderNumber = orderNumber,
                CustomerName = customerName,
                PhoneNumber = phoneNumber,
                Address = address,
                City = city,
                PostalCode = postalCode,
                Status = OrderStatus.Pending,
                PaymentMethod = paymentMethod,
                PaymentStatus = PaymentStatus.Pending,
                TotalAmount = subtotal - discountAmount,
                DiscountAmount = discountAmount,
                DiscountCode = discountCode,
                Notes = notes,
                CreatedAt = DateTime.Now
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Thêm các mục đơn hàng
            foreach (var item in cartItems)
            {
                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    ProductName = item.Product?.Name ?? "Không xác định",
                    Quantity = item.Quantity,
                    UnitPrice = item.Product?.Price ?? 0
                };

                _context.OrderItems.Add(orderItem);

                // Cập nhật tồn kho
                await _inventoryService.DeductStockAsync(item.ProductId, item.Quantity);
            }

            await _context.SaveChangesAsync();

            // Xóa giỏ hàng
            await _cartService.ClearCartAsync(userId);

            // Xóa giảm giá
            await _cartService.RemoveDiscountAsync(userId);

            return order;
        }

        public async Task<Order?> GetOrderAsync(int id)
        {
            return await _context.Orders
                .IncludeStandardReferences()
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<Order?> GetOrderByNumberAsync(string orderNumber)
        {
            return await _context.Orders
                .IncludeStandardReferences()
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
        }

        public async Task<IEnumerable<Order>> GetUserOrdersAsync(string userId)
        {
            return await _context.Orders
                .IncludeStandardReferences()
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetAllOrdersAsync(OrderStatus? status = null)
        {
            var query = _context.Orders
                .IncludeStandardReferences()
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            return await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task UpdateOrderStatusAsync(int id, OrderStatus status)
        {
            var order = await _context.Orders.FindAsync(id)
                ?? throw new ArgumentException("Đơn hàng không tồn tại");

            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;

            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            // Gửi email thông báo cập nhật trạng thái đơn hàng
            await _emailService.SendOrderStatusUpdateAsync(id);
        }

        public async Task UpdatePaymentStatusAsync(int id, PaymentStatus status, string? transactionId = null)
        {
            var order = await _context.Orders.FindAsync(id)
                ?? throw new ArgumentException("Đơn hàng không tồn tại");

            order.PaymentStatus = status;
            order.TransactionId = transactionId;
            order.UpdatedAt = DateTime.UtcNow;

            if (status == PaymentStatus.Completed)
            {
                // Cập nhật thời gian thanh toán
                order.UpdatedAt = DateTime.UtcNow;
                
                // Nếu thanh toán thành công, cập nhật trạng thái đơn hàng thành Processing
                if (order.Status == OrderStatus.Pending)
                {
                    order.Status = OrderStatus.Processing;
                }
            }

            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> CancelOrderAsync(int id)
        {
            var order = await _context.Orders
                .IncludeStandardReferences()
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                throw new ArgumentException("Đơn hàng không tồn tại");
            }

            // Chỉ cho phép hủy đơn hàng ở trạng thái Pending hoặc Processing
            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Processing)
            {
                return false;
            }

            // Cập nhật trạng thái đơn hàng
            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;

            // Hoàn lại tồn kho
            foreach (var item in order.OrderItems)
            {
                await _inventoryService.RestockAsync(item.ProductId, item.Quantity);
            }

            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            // Gửi email thông báo hủy đơn hàng
            await _emailService.SendOrderStatusUpdateAsync(id);

            return true;
        }

        public async Task<byte[]> GenerateInvoicePdfAsync(int orderId)
        {
            return await _pdfService.GenerateInvoicePdfAsync(orderId);
        }

        public async Task SendOrderConfirmationEmailAsync(int orderId)
        {
            await _emailService.SendOrderConfirmationAsync(orderId);
        }

        private static string GenerateOrderNumber()
        {
            // Tạo mã đơn hàng theo định dạng: CPT-yyyyMMdd-XXXX (X là số ngẫu nhiên)
            string prefix = "CPT";
            string datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            string randomPart = new Random().Next(1000, 9999).ToString();

            return $"{prefix}-{datePart}-{randomPart}";
        }

        // Các phương thức bổ sung cho Dashboard
        public async Task<int> GetTotalOrderCountAsync()
        {
            return await _context.Orders.CountAsync();
        }
        
        public async Task<int> GetTotalOrderCountAsync(string? searchTerm, string? status, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.Orders.AsQueryable();
            
            // Áp dụng bộ lọc tìm kiếm nếu có
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(o => 
                    o.OrderNumber.Contains(searchTerm) ||
                    o.CustomerName.Contains(searchTerm) ||
                    o.PhoneNumber.Contains(searchTerm) ||
                    o.Email != null && o.Email.Contains(searchTerm));
            }
            
            // Lọc theo trạng thái nếu có
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, out var orderStatus))
            {
                query = query.Where(o => o.Status == orderStatus);
            }
            
            // Lọc theo ngày nếu có
            if (fromDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= fromDate.Value);
            }
            
            if (toDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt <= toDate.Value.AddDays(1).AddSeconds(-1));
            }
            
            return await query.CountAsync();
        }

        public async Task<IEnumerable<Order>> GetRecentOrdersAsync(int count)
        {
            return await _context.Orders
                .IncludeStandardReferences()
                .OrderByDescending(o => o.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy danh sách tóm tắt đơn hàng sử dụng projection để tối ưu hóa truy vấn
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
                .Select(o => new OrderSummaryDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    CustomerName = o.CustomerName,
                    PhoneNumber = o.PhoneNumber,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    PaymentStatus = o.PaymentStatus,
                    CreatedAt = o.CreatedAt,
                    ItemCount = o.OrderItems.Count()
                })
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        
        public async Task<Dictionary<string, decimal>> GetMonthlyRevenueAsync(int months)
        {
            var result = new Dictionary<string, decimal>();
            var today = DateTime.Today;
            var startDate = today.AddMonths(-months + 1).AddDays(-(today.Day - 1)); // Đầu tháng

            for (int i = 0; i < months; i++)
            {
                var monthStart = startDate.AddMonths(i);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1); // Cuối tháng
                var monthName = monthStart.ToString("MMM yyyy"); // Tên tháng và năm

                var monthlyOrders = await _context.Orders
                    .Where(o => o.CreatedAt >= monthStart && o.CreatedAt <= monthEnd && o.PaymentStatus == PaymentStatus.Completed)
                    .ToListAsync();

                var revenue = monthlyOrders.Sum(o => o.TotalAmount);
                result.Add(monthName, revenue);
            }

            return result;
        }

        public async Task<IEnumerable<Order>> GetOrdersInPeriodAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<TopCustomerData>> GetTopCustomersAsync(DateTime startDate, DateTime endDate, int count)
        {
            var topCustomers = await _context.Orders
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate && o.UserId != null)
                .GroupBy(o => new { o.UserId, CustomerName = o.CustomerName, Email = o.Email })
                .Select(g => new TopCustomerData
                {
                    UserId = g.Key.UserId,
                    CustomerName = g.Key.CustomerName,
                    Email = g.Key.Email ?? string.Empty,
                    OrderCount = g.Count(),
                    TotalSpent = g.Sum(o => o.TotalAmount)
                })
                .OrderByDescending(c => c.TotalSpent)
                .Take(count)
                .ToListAsync();

            return topCustomers;
        }
        
        // Triển khai các phương thức còn thiếu từ interface IOrderService
        public async Task<Order?> GetOrderByIdAsync(int id)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id);
        }
        
        public async Task<IEnumerable<Order>> GetOrdersAsync(string? searchTerm, string? status, DateTime? fromDate, DateTime? toDate, int page, int pageSize)
        {
            var query = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .AsQueryable();
                
            // Áp dụng bộ lọc tìm kiếm nếu có
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(o => 
                    o.OrderNumber.Contains(searchTerm) ||
                    o.CustomerName.Contains(searchTerm) ||
                    o.PhoneNumber.Contains(searchTerm) ||
                    (o.Email != null && o.Email.Contains(searchTerm)));
            }
            
            // Lọc theo trạng thái nếu có
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, out var orderStatus))
            {
                query = query.Where(o => o.Status == orderStatus);
            }
            
            // Lọc theo ngày nếu có
            if (fromDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= fromDate.Value);
            }
            
            if (toDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt <= toDate.Value.AddDays(1).AddSeconds(-1));
            }
            
            // Phân trang
            return await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        
        public async Task UpdateOrderAsync(Order order)
        {
            _context.Entry(order).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }
        
        public async Task DeleteOrderAsync(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order != null)
            {
                // Xóa các mục đơn hàng liên quan
                var orderItems = await _context.OrderItems.Where(oi => oi.OrderId == id).ToListAsync();
                _context.OrderItems.RemoveRange(orderItems);
                
                // Xóa đơn hàng
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
            }
        }
    }
}