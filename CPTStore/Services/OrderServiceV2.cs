using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
            // Kiểm tra xem đã có giao dịch hiện có chưa
            var hasExistingTransaction = _context.Database.CurrentTransaction != null;
            IDbContextTransaction? transaction = null;

            try
            {
                // Chỉ bắt đầu giao dịch mới nếu chưa có giao dịch hiện có
                if (!hasExistingTransaction)
                {
                    transaction = await _context.Database.BeginTransactionAsync();
                    _logger.LogInformation("Đã bắt đầu giao dịch mới cho đơn hàng");
                }
                else
                {
                    _logger.LogInformation("Sử dụng giao dịch hiện có cho đơn hàng");
                }

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
                _logger.LogInformation($"Đã thêm mục đơn hàng: {orderItem.ProductName}, Số lượng: {orderItem.Quantity}");

                // Cập nhật tồn kho
                if (!await _inventoryService.DeductStockAsync(item.ProductId, item.Quantity))
                {
                    // Nếu không thể cập nhật tồn kho, hủy giao dịch
                    throw new InvalidOperationException($"Không thể cập nhật tồn kho cho sản phẩm '{item.Product?.Name ?? "Không xác định"}' (ID: {item.ProductId}).");
                }
                _logger.LogInformation($"Đã cập nhật tồn kho cho sản phẩm: {item.Product?.Name ?? "Không xác định"} (ID: {item.ProductId})");
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Đã lưu tất cả các mục đơn hàng");

            // Xóa giỏ hàng
            await _cartService.ClearCartAsync(userId);
            _logger.LogInformation("Đã xóa giỏ hàng");

            // Xóa mã giảm giá đã sử dụng
            if (cartDiscount != null)
            {
                _context.CartDiscounts.Remove(cartDiscount);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã xóa mã giảm giá đã sử dụng");
            }

            // Chỉ commit giao dịch nếu chúng ta đã tạo giao dịch mới và không được gọi từ controller
            // Controller sẽ quản lý giao dịch ở mức cao hơn
            if (!hasExistingTransaction && transaction != null)
            {
                await transaction.CommitAsync();
                _logger.LogInformation("Đã hoàn tất giao dịch tạo đơn hàng");
            }
            else
            {
                _logger.LogInformation("Không commit giao dịch vì đang sử dụng giao dịch hiện có từ controller");
            }

            return order;
        }
        catch (Exception ex)
        {
            // Nếu có lỗi và chúng ta đã tạo giao dịch mới, hủy giao dịch
            if (!hasExistingTransaction && transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Đã hủy giao dịch do lỗi: {ex.Message}");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError($"Lỗi khi hủy giao dịch: {rollbackEx.Message}");
                    if (rollbackEx.InnerException != null)
                    {
                        _logger.LogError($"Inner Exception khi hủy giao dịch: {rollbackEx.InnerException.Message}");
                    }
                }
            }
            else
            {
                _logger.LogWarning($"Không hủy giao dịch vì đang sử dụng giao dịch hiện có hoặc transaction là null");
            }
            
            _logger.LogError($"Lỗi khi tạo đơn hàng cho người dùng {userId}, Lỗi: {ex.Message}");
            if (ex.InnerException != null)
            {
                _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
            }
            _logger.LogError($"Stack Trace: {ex.StackTrace}");
            
            // Kiểm tra xem ngoại lệ có phải là InvalidOperationException từ khối try bên trong không
            if (ex is InvalidOperationException && ex.InnerException != null && ex.InnerException.Message.Contains("SqlTransaction has completed"))
            {
                // Đây là lỗi giao dịch đã hoàn thành, ghi log chi tiết hơn
                _logger.LogError("Phát hiện lỗi giao dịch đã hoàn thành, có thể do xung đột giữa các giao dịch");
            }
            
            throw new OrderServiceException($"Lỗi khi tạo đơn hàng: {ex.Message}", OrderErrorCode.GeneralError, ex);
        }
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
            // Kiểm tra xem đã có giao dịch hiện có chưa
            var hasExistingTransaction = _context.Database.CurrentTransaction != null;
            IDbContextTransaction? transaction = null;

            try
            {
                // Chỉ bắt đầu giao dịch mới nếu chưa có giao dịch hiện có
                if (!hasExistingTransaction)
                {
                    transaction = await _context.Database.BeginTransactionAsync();
                    _logger.LogInformation($"Đã bắt đầu giao dịch mới cho cập nhật đơn hàng ID: {order.Id}");
                }
                else
                {
                    _logger.LogInformation($"Sử dụng giao dịch hiện có cho cập nhật đơn hàng ID: {order.Id}");
                }

                await _orderRepository.UpdateAsync(order);

                // Hoàn tất giao dịch nếu chúng ta đã tạo giao dịch mới
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.CommitAsync();
                    _logger.LogInformation($"Đã hoàn tất giao dịch cập nhật đơn hàng ID: {order.Id}");
                }
            }
            catch (Exception ex)
            {
                // Nếu có lỗi và chúng ta đã tạo giao dịch mới, hủy giao dịch
                if (!hasExistingTransaction && transaction != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError($"Đã hủy giao dịch do lỗi: {ex.Message}");
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError($"Lỗi khi hủy giao dịch: {rollbackEx.Message}");
                        if (rollbackEx.InnerException != null)
                        {
                            _logger.LogError($"Inner Exception khi hủy giao dịch: {rollbackEx.InnerException.Message}");
                        }
                    }
                }

                _logger.LogError($"Lỗi khi cập nhật đơn hàng ID: {order.Id}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                }
                _logger.LogError($"Stack Trace: {ex.StackTrace}");

                throw new OrderServiceException($"Lỗi khi cập nhật đơn hàng: {ex.Message}", OrderErrorCode.GeneralError, ex);
            }
        }

        /// <summary>
        /// Cập nhật trạng thái đơn hàng
        /// </summary>
        public async Task UpdateOrderStatusAsync(int id, OrderStatus status)
        {
            // Kiểm tra xem đã có giao dịch hiện có chưa
            var hasExistingTransaction = _context.Database.CurrentTransaction != null;
            IDbContextTransaction? transaction = null;

            try
            {
                // Chỉ bắt đầu giao dịch mới nếu chưa có giao dịch hiện có
                if (!hasExistingTransaction)
                {
                    transaction = await _context.Database.BeginTransactionAsync();
                    _logger.LogInformation($"Đã bắt đầu giao dịch mới cho cập nhật trạng thái đơn hàng ID: {id}");
                }
                else
                {
                    _logger.LogInformation($"Sử dụng giao dịch hiện có cho cập nhật trạng thái đơn hàng ID: {id}");
                }

                var order = await _orderRepository.GetByIdAsync(id);
                if (order == null)
                {
                    throw new OrderServiceException("Không tìm thấy đơn hàng", OrderErrorCode.OrderNotFound);
                }

                order.Status = status;
                order.UpdatedAt = DateTime.Now;

                await _orderRepository.UpdateAsync(order);

                // Chỉ commit giao dịch nếu chúng ta đã tạo giao dịch mới và không được gọi từ controller
                // Controller sẽ quản lý giao dịch ở mức cao hơn
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.CommitAsync();
                    _logger.LogInformation($"Đã hoàn tất giao dịch cập nhật trạng thái đơn hàng ID: {id}");
                }
                else
                {
                    _logger.LogInformation($"Không commit giao dịch vì đang sử dụng giao dịch hiện có từ controller");
                }
            }
            catch (Exception ex)
            {
                // Nếu có lỗi và chúng ta đã tạo giao dịch mới, hủy giao dịch
                if (!hasExistingTransaction && transaction != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError($"Đã hủy giao dịch do lỗi: {ex.Message}");
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError($"Lỗi khi hủy giao dịch: {rollbackEx.Message}");
                        if (rollbackEx.InnerException != null)
                        {
                            _logger.LogError($"Inner Exception khi hủy giao dịch: {rollbackEx.InnerException.Message}");
                        }
                    }
                }

                _logger.LogError($"Lỗi khi cập nhật trạng thái đơn hàng ID: {id}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                }
                _logger.LogError($"Stack Trace: {ex.StackTrace}");

                throw new OrderServiceException($"Lỗi khi cập nhật trạng thái đơn hàng: {ex.Message}", OrderErrorCode.GeneralError, ex);
            }
        }

        /// <summary>
        /// Cập nhật trạng thái thanh toán
        /// </summary>
        public async Task UpdatePaymentStatusAsync(int id, PaymentStatus status, string? transactionId = null)
        {
            // Kiểm tra xem đã có giao dịch hiện có chưa
            var hasExistingTransaction = _context.Database.CurrentTransaction != null;
            IDbContextTransaction? transaction = null;

            try
            {
                // Chỉ bắt đầu giao dịch mới nếu chưa có giao dịch hiện có
                if (!hasExistingTransaction)
                {
                    transaction = await _context.Database.BeginTransactionAsync();
                    _logger.LogInformation($"Đã bắt đầu giao dịch mới cho cập nhật trạng thái thanh toán đơn hàng ID: {id}");
                }
                else
                {
                    _logger.LogInformation($"Sử dụng giao dịch hiện có cho cập nhật trạng thái thanh toán đơn hàng ID: {id}");
                }

                var order = await _orderRepository.GetByIdAsync(id);
                if (order == null)
                {
                    throw new OrderServiceException("Không tìm thấy đơn hàng", OrderErrorCode.OrderNotFound);
                }

                order.PaymentStatus = status;
                order.TransactionId = transactionId;
                order.UpdatedAt = DateTime.Now;

                await _orderRepository.UpdateAsync(order);

                // Hoàn tất giao dịch nếu chúng ta đã tạo giao dịch mới
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.CommitAsync();
                    _logger.LogInformation($"Đã hoàn tất giao dịch cập nhật trạng thái thanh toán đơn hàng ID: {id}");
                }
            }
            catch (Exception ex)
            {
                // Nếu có lỗi và chúng ta đã tạo giao dịch mới, hủy giao dịch
                if (!hasExistingTransaction && transaction != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError($"Đã hủy giao dịch do lỗi: {ex.Message}");
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError($"Lỗi khi hủy giao dịch: {rollbackEx.Message}");
                        if (rollbackEx.InnerException != null)
                        {
                            _logger.LogError($"Inner Exception khi hủy giao dịch: {rollbackEx.InnerException.Message}");
                        }
                    }
                }

                _logger.LogError($"Lỗi khi cập nhật trạng thái thanh toán đơn hàng ID: {id}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                }
                _logger.LogError($"Stack Trace: {ex.StackTrace}");

                throw new OrderServiceException($"Lỗi khi cập nhật trạng thái thanh toán đơn hàng: {ex.Message}", OrderErrorCode.GeneralError, ex);
            }
        }

        /// <summary>
        /// Hủy đơn hàng với ID được chỉ định
        /// </summary>
        public async Task<bool> CancelOrderAsync(int id)
        {
            return await CancelOrderAsync(id, userId: null);
        }

        /// <summary>
        /// Hủy đơn hàng với ID và userId được chỉ định
        /// </summary>
        public async Task<bool> CancelOrderAsync(int id, string? userId)
        {
            // Kiểm tra xem đã có giao dịch hiện có chưa
            var hasExistingTransaction = _context.Database.CurrentTransaction != null;
            IDbContextTransaction? transaction = null;

            try
            {
                // Chỉ bắt đầu giao dịch mới nếu chưa có giao dịch hiện có
                if (!hasExistingTransaction)
                {
                    transaction = await _context.Database.BeginTransactionAsync();
                    _logger.LogInformation($"Đã bắt đầu giao dịch mới cho hủy đơn hàng ID: {id}");
                }
                else
                {
                    _logger.LogInformation($"Sử dụng giao dịch hiện có cho hủy đơn hàng ID: {id}");
                }

                var order = await _orderRepository.GetByIdAsync(id);
                if (order == null)
                {
                    _logger.LogWarning($"Không tìm thấy đơn hàng ID: {id} để hủy");
                    return false;
                }

                // Chỉ cho phép hủy đơn hàng ở trạng thái Pending hoặc Processing
                if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Processing)
                {
                    _logger.LogWarning($"Không thể hủy đơn hàng ID: {id} vì trạng thái hiện tại là {order.Status}");
                    return false;
                }

                // Cập nhật trạng thái đơn hàng
                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.Now;

                // Hoàn trả số lượng vào kho
                foreach (var item in order.OrderItems)
                {
                    if (!await _inventoryService.RestockAsync(item.ProductId, item.Quantity))
                    {
                        throw new InvalidOperationException($"Không thể hoàn trả số lượng vào kho cho sản phẩm ID: {item.ProductId}");
                    }
                    _logger.LogInformation($"Đã hoàn trả {item.Quantity} sản phẩm ID: {item.ProductId} vào kho");
                }

                await _orderRepository.UpdateAsync(order);
                _logger.LogInformation($"Đã cập nhật trạng thái đơn hàng ID: {id} thành Cancelled");

                // Chỉ commit giao dịch nếu chúng ta đã tạo giao dịch mới và không được gọi từ controller
                // Controller sẽ quản lý giao dịch ở mức cao hơn
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.CommitAsync();
                    _logger.LogInformation($"Đã hoàn tất giao dịch hủy đơn hàng ID: {id}");
                }

                return true;
            }
            catch (Exception ex)
            {
                // Nếu có lỗi và chúng ta đã tạo giao dịch mới, hủy giao dịch
                if (!hasExistingTransaction && transaction != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError($"Đã hủy giao dịch do lỗi: {ex.Message}");
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError($"Lỗi khi hủy giao dịch: {rollbackEx.Message}");
                        if (rollbackEx.InnerException != null)
                        {
                            _logger.LogError($"Inner Exception khi hủy giao dịch: {rollbackEx.InnerException.Message}");
                        }
                    }
                }

                _logger.LogError($"Lỗi khi hủy đơn hàng ID: {id}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                }
                _logger.LogError($"Stack Trace: {ex.StackTrace}");

                throw new OrderServiceException($"Lỗi khi hủy đơn hàng: {ex.Message}", OrderErrorCode.GeneralError, ex);
            }
        }

        /// <summary>
        /// Xóa đơn hàng
        /// </summary>
        public async Task DeleteOrderAsync(int id)
        {
            // Kiểm tra xem đã có giao dịch hiện có chưa
            var hasExistingTransaction = _context.Database.CurrentTransaction != null;
            IDbContextTransaction? transaction = null;

            try
            {
                // Chỉ bắt đầu giao dịch mới nếu chưa có giao dịch hiện có
                if (!hasExistingTransaction)
                {
                    transaction = await _context.Database.BeginTransactionAsync();
                    _logger.LogInformation($"Đã bắt đầu giao dịch mới cho xóa đơn hàng ID: {id}");
                }
                else
                {
                    _logger.LogInformation($"Sử dụng giao dịch hiện có cho xóa đơn hàng ID: {id}");
                }

                await _orderRepository.DeleteAsync(id);

                // Hoàn tất giao dịch nếu chúng ta đã tạo giao dịch mới
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.CommitAsync();
                    _logger.LogInformation($"Đã hoàn tất giao dịch xóa đơn hàng ID: {id}");
                }
            }
            catch (Exception ex)
            {
                // Nếu có lỗi và chúng ta đã tạo giao dịch mới, hủy giao dịch
                if (!hasExistingTransaction && transaction != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError($"Đã hủy giao dịch do lỗi: {ex.Message}");
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError($"Lỗi khi hủy giao dịch: {rollbackEx.Message}");
                        if (rollbackEx.InnerException != null)
                        {
                            _logger.LogError($"Inner Exception khi hủy giao dịch: {rollbackEx.InnerException.Message}");
                        }
                    }
                }

                _logger.LogError($"Lỗi khi xóa đơn hàng ID: {id}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                }
                _logger.LogError($"Stack Trace: {ex.StackTrace}");

                throw new OrderServiceException($"Lỗi khi xóa đơn hàng: {ex.Message}", OrderErrorCode.GeneralError, ex);
            }
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
                    UserId = g.Key.UserId ?? string.Empty,
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