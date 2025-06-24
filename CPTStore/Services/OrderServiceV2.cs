using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using CPTStore.Areas.Admin.ViewModels;
using CPTStore.Extensions;
using CPTStore.ViewModels;
using CPTStore.Repositories.Interfaces;
using CPTStore.Exceptions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CPTStore.Services
{
    /// <summary>
    /// Phiên bản cải tiến của OrderService sử dụng Repository Pattern
    /// </summary>
    public class OrderServiceV2 : IOrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly IOrderRepository _orderRepository;
        private readonly ICartService _cartService;
        private readonly IInventoryService _inventoryService;
        private readonly IPdfService _pdfService;
        private readonly IEmailService _emailService;
        private readonly ILogger<OrderServiceV2> _logger;

        public OrderServiceV2(
            ApplicationDbContext context,
            IOrderRepository orderRepository,
            ICartService cartService,
            IInventoryService inventoryService,
            IPdfService pdfService,
            IEmailService emailService,
            ILogger<OrderServiceV2> logger)
        {
            _context = context;
            _orderRepository = orderRepository;
            _cartService = cartService;
            _inventoryService = inventoryService;
            _pdfService = pdfService;
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Tạo đơn hàng mới
        /// </summary>
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
                throw new OrderServiceException("Giỏ hàng trống", OrderErrorCode.EmptyCart);
            }

            // Kiểm tra tồn kho
            foreach (var item in cartItems)
            {
                if (!await _inventoryService.IsInStockAsync(item.ProductId, item.Quantity))
                {
                    throw new OrderServiceException($"Sản phẩm '{item.Product?.Name ?? "Không xác định"}' không đủ số lượng trong kho", OrderErrorCode.InsufficientInventory);
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

            // Thêm đơn hàng vào cơ sở dữ liệu
            await _orderRepository.AddAsync(order);

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

            // Xóa mã giảm giá đã sử dụng
            if (cartDiscount != null)
            {
                _context.CartDiscounts.Remove(cartDiscount);
                await _context.SaveChangesAsync();
            }

            return order;
        }

        /// <summary>
        /// Lấy đơn hàng theo ID
        /// </summary>
        public async Task<Order?> GetOrderAsync(int id)
        {
            return await _orderRepository.GetByIdAsync(id);
        }

        /// <summary>
        /// Lấy đơn hàng theo ID (phương thức thay thế)
        /// </summary>
        public async Task<Order?> GetOrderByIdAsync(int id)
        {
            return await _orderRepository.GetByIdAsync(id);
        }

        /// <summary>
        /// Lấy đơn hàng theo số đơn hàng
        /// </summary>
        public async Task<Order?> GetOrderByNumberAsync(string orderNumber)
        {
            return await _orderRepository.GetByOrderNumberAsync(orderNumber);
        }

        /// <summary>
        /// Lấy tất cả đơn hàng của một người dùng
        /// </summary>
        public async Task<IEnumerable<Order>> GetUserOrdersAsync(string userId)
        {
            return await _orderRepository.GetByUserIdAsync(userId);
        }

        /// <summary>
        /// Lấy tất cả đơn hàng
        /// </summary>
        public async Task<IEnumerable<Order>> GetAllOrdersAsync(OrderStatus? status = null)
        {
            return await _orderRepository.GetAllAsync(status);
        }

        /// <summary>
        /// Lấy đơn hàng theo điều kiện tìm kiếm
        /// </summary>
        public async Task<IEnumerable<Order>> GetOrdersAsync(string? searchTerm, string? status, DateTime? fromDate, DateTime? toDate, int page, int pageSize)
        {
            return await _orderRepository.GetOrdersAsync(searchTerm, status, fromDate, toDate, page, pageSize);
        }

        /// <summary>
        /// Cập nhật đơn hàng
        /// </summary>
        public async Task UpdateOrderAsync(Order order)
        {
            await _orderRepository.UpdateAsync(order);
        }

        /// <summary>
        /// Cập nhật trạng thái đơn hàng
        /// </summary>
        public async Task UpdateOrderStatusAsync(int id, OrderStatus status)
        {
            var order = await _orderRepository.GetByIdAsync(id);
            if (order == null)
            {
                throw new OrderServiceException("Không tìm thấy đơn hàng", OrderErrorCode.OrderNotFound);
            }

            order.Status = status;
            order.UpdatedAt = DateTime.Now;

            await _orderRepository.UpdateAsync(order);
        }

        /// <summary>
        /// Cập nhật trạng thái thanh toán
        /// </summary>
        public async Task UpdatePaymentStatusAsync(int id, PaymentStatus status, string? transactionId = null)
        {
            var order = await _orderRepository.GetByIdAsync(id);
            if (order == null)
            {
                throw new OrderServiceException("Không tìm thấy đơn hàng", OrderErrorCode.OrderNotFound);
            }

            order.PaymentStatus = status;
            order.TransactionId = transactionId;
            order.UpdatedAt = DateTime.Now;

            await _orderRepository.UpdateAsync(order);
        }

        /// <summary>
        /// Hủy đơn hàng
        /// </summary>
        public async Task<bool> CancelOrderAsync(int id)
        {
            var order = await _orderRepository.GetByIdAsync(id);
            if (order == null)
            {
                return false;
            }

            // Chỉ cho phép hủy đơn hàng ở trạng thái Pending hoặc Processing
            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Processing)
            {
                return false;
            }

            // Cập nhật trạng thái đơn hàng
            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.Now;

            // Hoàn trả số lượng vào kho
            foreach (var item in order.OrderItems)
            {
                await _inventoryService.RestockAsync(item.ProductId, item.Quantity);
            }

            await _orderRepository.UpdateAsync(order);
            return true;
        }

        /// <summary>
        /// Xóa đơn hàng
        /// </summary>
        public async Task DeleteOrderAsync(int id)
        {
            await _orderRepository.DeleteAsync(id);
        }

        /// <summary>
        /// Tạo file PDF hóa đơn
        /// </summary>
        public async Task<byte[]> GenerateInvoicePdfAsync(int orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                throw new OrderServiceException("Không tìm thấy đơn hàng", OrderErrorCode.OrderNotFound);
            }

            return await _pdfService.GenerateInvoicePdfAsync(orderId);
        }

        /// <summary>
        /// Gửi email xác nhận đơn hàng
        /// </summary>
        public async Task SendOrderConfirmationEmailAsync(int orderId)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                throw new OrderServiceException("Không tìm thấy đơn hàng", OrderErrorCode.OrderNotFound);
            }

            await _emailService.SendOrderConfirmationAsync(orderId);
        }

        /// <summary>
        /// Lấy tổng số đơn hàng
        /// </summary>
        public async Task<int> GetTotalOrderCountAsync()
        {
            return await _orderRepository.GetTotalCountAsync();
        }

        /// <summary>
        /// Lấy tổng số đơn hàng theo điều kiện tìm kiếm
        /// </summary>
        public async Task<int> GetTotalOrderCountAsync(string? searchTerm, string? status, DateTime? fromDate, DateTime? toDate)
        {
            return await _orderRepository.GetTotalCountAsync(searchTerm, status, fromDate, toDate);
        }

        /// <summary>
        /// Lấy các đơn hàng gần đây
        /// </summary>
        public async Task<IEnumerable<Order>> GetRecentOrdersAsync(int count)
        {
            return await _orderRepository.GetRecentOrdersAsync(count);
        }

        /// <summary>
        /// Lấy thông tin tóm tắt của các đơn hàng
        /// </summary>
        public async Task<IEnumerable<OrderSummaryDto>> GetOrderSummariesAsync(OrderStatus? status = null)
        {
            return await _orderRepository.GetOrderSummariesAsync(status);
        }

        /// <summary>
        /// Lấy doanh thu theo tháng
        /// </summary>
        public async Task<Dictionary<string, decimal>> GetMonthlyRevenueAsync(int months)
        {
            try
            {
                var result = new Dictionary<string, decimal>();
                var endDate = DateTime.Now;
                var startDate = endDate.AddMonths(-months + 1).Date.AddDays(-(endDate.Day - 1)); // Đầu tháng

                var orders = await _orderRepository.GetOrdersInPeriodAsync(startDate, endDate);

                // Kiểm tra nếu không có đơn hàng nào
                if (orders == null || !orders.Any())
                {
                    // Tạo danh sách tất cả các tháng trong khoảng thời gian với doanh thu bằng 0
                    for (int i = 0; i < months; i++)
                    {
                        var date = endDate.AddMonths(-i);
                        var monthYear = $"{date.Month}/{date.Year}";
                        result[monthYear] = 0;
                    }
                    return result;
                }

                // Nhóm theo tháng và tính tổng doanh thu
                var groupedOrders = orders
                    .Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Refunded)
                    .GroupBy(o => new { Month = o.CreatedAt.Month, Year = o.CreatedAt.Year })
                    .Select(g => new
                    {
                        MonthYear = $"{g.Key.Month}/{g.Key.Year}",
                        Revenue = g.Sum(o => o.TotalAmount)
                    });

                // Tạo danh sách tất cả các tháng trong khoảng thời gian
                for (int i = 0; i < months; i++)
                {
                    var date = endDate.AddMonths(-i);
                    var monthYear = $"{date.Month}/{date.Year}";
                    result[monthYear] = 0; // Khởi tạo với giá trị 0
                }

                // Cập nhật doanh thu cho các tháng có dữ liệu
                foreach (var item in groupedOrders)
                {
                    result[item.MonthYear] = item.Revenue;
                }

                return result;
            }
            catch (Exception ex)
            {
                // Ghi log lỗi
                _logger.LogError(ex, "Lỗi khi lấy doanh thu theo tháng: {Message}", ex.Message);
                
                // Trả về Dictionary trống thay vì ném ngoại lệ
                var result = new Dictionary<string, decimal>();
                var endDate = DateTime.Now;
                
                // Tạo danh sách tất cả các tháng trong khoảng thời gian với doanh thu bằng 0
                for (int i = 0; i < months; i++)
                {
                    var date = endDate.AddMonths(-i);
                    var monthYear = $"{date.Month}/{date.Year}";
                    result[monthYear] = 0;
                }
                
                return result;
            }
        }

        /// <summary>
        /// Lấy các đơn hàng trong một khoảng thời gian
        /// </summary>
        public async Task<IEnumerable<Order>> GetOrdersInPeriodAsync(DateTime startDate, DateTime endDate)
        {
            return await _orderRepository.GetOrdersInPeriodAsync(startDate, endDate);
        }

        /// <summary>
        /// Lấy danh sách khách hàng mua nhiều nhất
        /// </summary>
        public async Task<IEnumerable<TopCustomerData>> GetTopCustomersAsync(DateTime startDate, DateTime endDate, int count)
        {
            var orders = await _orderRepository.GetOrdersInPeriodAsync(startDate, endDate);

            var topCustomers = orders
                .Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Refunded)
                .GroupBy(o => new { o.UserId, o.CustomerName })
                .Select(g => new TopCustomerData
                {
                    UserId = g.Key.UserId,
                    CustomerName = g.Key.CustomerName,
                    Email = g.FirstOrDefault()?.Email ?? string.Empty,
                    OrderCount = g.Count(),
                    TotalSpent = g.Sum(o => o.TotalAmount)
                })
                .OrderByDescending(c => c.TotalSpent)
                .Take(count);

            return topCustomers;
        }

        /// <summary>
        /// Tạo mã đơn hàng ngẫu nhiên
        /// </summary>
        private string GenerateOrderNumber()
        {
            // Format: CPT-yyyyMMdd-XXXX (X là số ngẫu nhiên)
            string prefix = "CPT-" + DateTime.Now.ToString("yyyyMMdd") + "-";
            string randomPart = new Random().Next(1000, 9999).ToString();
            return prefix + randomPart;
        }
    }
}