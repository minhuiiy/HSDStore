using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using CPTStore.Areas.Admin.ViewModels;
using CPTStore.Extensions;
using CPTStore.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CPTStore.Services
{
    public class OrderService(
        ApplicationDbContext context,
        ICartService cartService,
        IInventoryService inventoryService,
        IPdfService pdfService,
        IEmailService emailService,
        IMembershipService membershipService,
        ILogger<OrderService> logger) : IOrderService
    {
        private readonly ApplicationDbContext _context = context;
        private readonly ICartService _cartService = cartService;
        private readonly IInventoryService _inventoryService = inventoryService;
        private readonly IPdfService _pdfService = pdfService;
        private readonly IEmailService _emailService = emailService;
        private readonly IMembershipService _membershipService = membershipService;
        private readonly ILogger<OrderService> _logger = logger;

        // Đối tượng khóa để đồng bộ hóa quá trình tạo đơn hàng
        private static readonly SemaphoreSlim _orderCreationSemaphore = new(1, 1);

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
            try
            {
                // Sử dụng khóa để đảm bảo chỉ có một luồng có thể tạo đơn hàng cho người dùng tại một thời điểm
                await _orderCreationSemaphore.WaitAsync();
                Console.WriteLine($"Bắt đầu tạo đơn hàng cho người dùng: {userId}");

                // Thực hiện toàn bộ quá trình tạo đơn hàng trong một giao dịch
                // Kiểm tra xem đã có giao dịch hiện tại chưa
                bool hasExistingTransaction = _context.Database.CurrentTransaction != null;
                Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
                
                if (!hasExistingTransaction)
                {
                    // Chỉ tạo giao dịch mới nếu chưa có giao dịch hiện tại
                    transaction = await _context.Database.BeginTransactionAsync();
                    Console.WriteLine("Đã tạo giao dịch mới cho quá trình tạo đơn hàng");
                }
                else
                {
                    // Sử dụng giao dịch hiện tại
                    transaction = _context.Database.CurrentTransaction;
                    Console.WriteLine("Sử dụng giao dịch hiện có cho quá trình tạo đơn hàng");
                }
                {
                    try
                    {
                        // Lấy giỏ hàng của người dùng
                        var cartItems = await _cartService.GetCartItemsAsync(userId);
                        if (cartItems.Count == 0)
                        {
                            throw new InvalidOperationException("Giỏ hàng trống");
                        }
                        Console.WriteLine($"Đã lấy {cartItems.Count} sản phẩm từ giỏ hàng của người dùng: {userId}");

                        // Tạo danh sách để lưu các sản phẩm không đủ tồn kho
                        var outOfStockItems = new List<string>();

                        // Kiểm tra tồn kho cho tất cả sản phẩm
                        Console.WriteLine("Bắt đầu kiểm tra tồn kho cho tất cả sản phẩm");
                        foreach (var item in cartItems)
                        {
                            if (!await _inventoryService.IsInStockAsync(item.ProductId, item.Quantity))
                            {
                                outOfStockItems.Add(item.Product?.Name ?? "Không xác định");
                                Console.WriteLine($"Sản phẩm '{item.Product?.Name ?? "Không xác định"}' (ID: {item.ProductId}) không đủ số lượng trong kho. Yêu cầu: {item.Quantity}");
                            }
                            else
                            {
                                Console.WriteLine($"Sản phẩm '{item.Product?.Name ?? "Không xác định"}' (ID: {item.ProductId}) có đủ số lượng trong kho. Yêu cầu: {item.Quantity}");
                            }
                        }

                        // Nếu có sản phẩm không đủ tồn kho, ném ngoại lệ với danh sách đầy đủ
                        if (outOfStockItems.Count > 0)
                        {
                            throw new InvalidOperationException($"Các sản phẩm sau không đủ số lượng trong kho: {string.Join(", ", outOfStockItems)}");
                        }

                        // Tính tổng tiền
                        decimal subtotal = await _cartService.GetCartTotalAsync(userId);
                        Console.WriteLine($"Tổng tiền đơn hàng: {subtotal}");
                        
                        // Lấy thông tin giảm giá (nếu có)
                        var cartDiscount = await _context.CartDiscounts
                            .FirstOrDefaultAsync(cd => cd.UserId == userId);
                        
                        decimal discountAmount = cartDiscount?.DiscountAmount ?? 0;
                        string? discountCode = cartDiscount?.DiscountCode;
                        Console.WriteLine($"Giảm giá: {discountAmount} (Mã: {discountCode ?? "Không có"})");

                        // Tạo mã đơn hàng
                        string orderNumber = GenerateOrderNumber();
                        Console.WriteLine($"Đã tạo mã đơn hàng: {orderNumber}");

                        // Kiểm tra xem userId có tồn tại trong bảng AspNetUsers không
                        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                        
                        // Tạo đơn hàng mới
                        var order = new Order
                        {
                            // Nếu userId không tồn tại trong bảng AspNetUsers, đặt UserId = null
                            UserId = userExists ? userId : null,
                            // Lưu SessionId khi người dùng chưa đăng nhập
                            SessionId = !userExists ? userId : null,
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

                        // Thêm đơn hàng vào context
                        _context.Orders.Add(order);
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"Đã lưu đơn hàng với ID: {order.Id}");
                        
                        // Thêm các mục đơn hàng và cập nhật tồn kho
                        Console.WriteLine("Bắt đầu thêm các mục đơn hàng và cập nhật tồn kho");
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
                            Console.WriteLine($"Đã thêm mục đơn hàng: {orderItem.ProductName}, Số lượng: {orderItem.Quantity}, Đơn giá: {orderItem.UnitPrice}");

                            // Cập nhật tồn kho
                            if (!await _inventoryService.DeductStockAsync(item.ProductId, item.Quantity))
                            {
                                // Nếu không thể cập nhật tồn kho, hủy giao dịch
                                throw new InvalidOperationException($"Không thể cập nhật tồn kho cho sản phẩm '{item.Product?.Name ?? "Không xác định"}' (ID: {item.ProductId}).");
                            }
                            Console.WriteLine($"Đã cập nhật tồn kho cho sản phẩm: {item.Product?.Name ?? "Không xác định"} (ID: {item.ProductId})");
                        }

                        await _context.SaveChangesAsync();
                        Console.WriteLine("Đã lưu tất cả các mục đơn hàng");

                        // Xóa giỏ hàng
                        await _cartService.ClearCartAsync(userId);
                        Console.WriteLine("Đã xóa giỏ hàng");

                        // Xóa giảm giá
                        await _cartService.RemoveDiscountAsync(userId);
                        Console.WriteLine("Đã xóa giảm giá");

                        // Hoàn tất giao dịch nếu chúng ta đã tạo giao dịch mới
                        if (!hasExistingTransaction && transaction != null)
                        {
                            await transaction.CommitAsync();
                            Console.WriteLine("Đã hoàn tất giao dịch tạo đơn hàng");
                        }
                        else
                        {
                            Console.WriteLine("Không commit giao dịch vì đang sử dụng giao dịch hiện có");
                        }

                        // Cập nhật cấp độ thành viên dựa trên tổng giá trị đơn hàng (chỉ cho người dùng đã đăng nhập)
                        if (userExists && !string.IsNullOrEmpty(userId))
                        {
                            try
                            {
                                await _membershipService.UpdateMembershipLevelAsync(userId, order.TotalAmount);
                                Console.WriteLine("Đã cập nhật cấp độ thành viên");
                            }
                            catch (Exception membershipEx)
                            {
                                _logger.LogWarning("Không thể cập nhật cấp độ thành viên: {Message}", membershipEx.Message);
                                Console.WriteLine($"Cảnh báo: Không thể cập nhật cấp độ thành viên: {membershipEx.Message}");
                                // Không ném ngoại lệ ở đây để đơn hàng vẫn được tạo thành công
                            }
                        }
                        else
                        {
                            Console.WriteLine("Bỏ qua cập nhật cấp độ thành viên vì đây là khách không đăng nhập");
                        }

                        Console.WriteLine($"Đã tạo đơn hàng thành công với mã: {orderNumber}");
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
                                Console.WriteLine($"Đã hủy giao dịch do lỗi: {ex.Message}");
                            }
                            catch (Exception rollbackEx)
                            {
                                Console.WriteLine($"Lỗi khi hủy giao dịch: {rollbackEx.Message}");
                                if (rollbackEx.InnerException != null)
                                {
                                    Console.WriteLine($"Inner Exception khi hủy giao dịch: {rollbackEx.InnerException.Message}");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Không hủy giao dịch vì đang sử dụng giao dịch hiện có hoặc transaction là null");
                        }
                        
                        Console.WriteLine($"Lỗi khi tạo đơn hàng cho người dùng {userId}, Lỗi: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        }
                        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                        throw new InvalidOperationException($"Lỗi khi tạo đơn hàng: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tạo đơn hàng cho người dùng {userId}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                
                // Kiểm tra xem ngoại lệ có phải là InvalidOperationException từ khối try bên trong không
                if (ex is InvalidOperationException && ex.InnerException != null && ex.InnerException.Message.Contains("SqlTransaction has completed"))
                {
                    // Đây là lỗi giao dịch đã hoàn thành, ghi log chi tiết hơn
                    Console.WriteLine("Phát hiện lỗi giao dịch đã hoàn thành, có thể do xung đột giữa các giao dịch");
                }
                
                throw new InvalidOperationException($"Lỗi khi tạo đơn hàng: {ex.Message}", ex);
            }
            finally
            {
                // Đảm bảo luôn giải phóng khóa
                _orderCreationSemaphore.Release();
                Console.WriteLine($"Đã giải phóng khóa tạo đơn hàng cho người dùng: {userId}");
            }
        }

        public async Task<Order?> GetOrderAsync(int id)
        {
            try
            {
                return await _context.Orders
                    .IncludeStandardReferences()
                    .FirstOrDefaultAsync(o => o.Id == id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy thông tin đơn hàng ID: {id}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Không thể lấy thông tin đơn hàng: {ex.Message}", ex);
            }
        }

        public async Task<Order?> GetOrderByNumberAsync(string orderNumber)
        {
            try
            {
                return await _context.Orders
                    .IncludeStandardReferences()
                    .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy thông tin đơn hàng số: {orderNumber}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Không thể lấy thông tin đơn hàng: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<Order>> GetUserOrdersAsync(string userId)
        {
            try
            {
                // Kiểm tra nếu userId là null hoặc rỗng
                if (string.IsNullOrEmpty(userId))
                {
                    // Trả về danh sách rỗng nếu userId không hợp lệ
                    return new List<Order>();
                }

                // Kiểm tra xem userId có tồn tại trong bảng AspNetUsers không
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);

                // Nếu userId tồn tại trong bảng AspNetUsers, tìm theo UserId
                // Nếu không, tìm theo SessionId
                return await _context.Orders
                    .IncludeStandardReferences()
                    .Where(o => (userExists && o.UserId == userId) || (!userExists && o.SessionId == userId))
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy danh sách đơn hàng của người dùng: {userId}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Không thể lấy danh sách đơn hàng: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<Order>> GetAllOrdersAsync(OrderStatus? status = null)
        {
            try
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
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy tất cả đơn hàng, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Không thể lấy danh sách đơn hàng: {ex.Message}", ex);
            }
        }

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
                    _logger.LogInformation($"Bắt đầu giao dịch mới để cập nhật trạng thái đơn hàng ID: {id}");
                }
                else
                {
                    _logger.LogInformation($"Sử dụng giao dịch hiện có để cập nhật trạng thái đơn hàng ID: {id}");
                }

                var order = await _context.Orders.FindAsync(id);
                if (order == null)
                {
                    _logger.LogWarning($"Không tìm thấy đơn hàng ID: {id} để cập nhật trạng thái");
                    throw new ArgumentException("Đơn hàng không tồn tại");
                }

                order.Status = status;
                order.UpdatedAt = DateTime.UtcNow;

                _context.Orders.Update(order);
                await _context.SaveChangesAsync();

                // Chỉ commit giao dịch nếu nó được tạo mới trong phương thức này
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.CommitAsync();
                    _logger.LogInformation($"Đã commit giao dịch cập nhật trạng thái đơn hàng ID: {id}");
                }

                // Gửi email thông báo cập nhật trạng thái đơn hàng
                try
                {
                    await _emailService.SendOrderStatusUpdateAsync(id);
                }
                catch (Exception emailEx)
                {
                    // Ghi log lỗi gửi email nhưng không ảnh hưởng đến việc cập nhật trạng thái
                    _logger.LogError(emailEx, $"Lỗi khi gửi email cập nhật trạng thái đơn hàng ID: {id}");
                }
            }
            catch (DbUpdateException dbEx)
            {
                // Chỉ rollback giao dịch nếu nó được tạo mới trong phương thức này
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(dbEx, $"Đã rollback giao dịch do lỗi cập nhật trạng thái đơn hàng ID: {id}");
                }

                _logger.LogError(dbEx, $"Lỗi cơ sở dữ liệu khi cập nhật trạng thái đơn hàng ID: {id}, Lỗi: {dbEx.Message}");
                throw new InvalidOperationException($"Không thể cập nhật trạng thái đơn hàng do lỗi cơ sở dữ liệu: {dbEx.Message}", dbEx);
            }
            catch (Exception ex)
            {
                // Chỉ rollback giao dịch nếu nó được tạo mới trong phương thức này
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Đã rollback giao dịch do lỗi cập nhật trạng thái đơn hàng ID: {id}");
                }

                _logger.LogError(ex, $"Lỗi khi cập nhật trạng thái đơn hàng ID: {id}, Lỗi: {ex.Message}");
                throw new InvalidOperationException($"Không thể cập nhật trạng thái đơn hàng: {ex.Message}", ex);
            }
        }

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
                    _logger.LogInformation($"Bắt đầu giao dịch mới để cập nhật trạng thái thanh toán đơn hàng ID: {id}");
                }
                else
                {
                    _logger.LogInformation($"Sử dụng giao dịch hiện có để cập nhật trạng thái thanh toán đơn hàng ID: {id}");
                }

                var order = await _context.Orders.FindAsync(id);
                if (order == null)
                {
                    _logger.LogWarning($"Không tìm thấy đơn hàng ID: {id} để cập nhật trạng thái thanh toán");
                    throw new ArgumentException("Đơn hàng không tồn tại");
                }

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

                // Chỉ commit giao dịch nếu nó được tạo mới trong phương thức này
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.CommitAsync();
                    _logger.LogInformation($"Đã commit giao dịch cập nhật trạng thái thanh toán đơn hàng ID: {id}");
                }
                
                // Ghi log thông tin cập nhật
                _logger.LogInformation($"Đã cập nhật trạng thái thanh toán đơn hàng ID: {id} thành {status}, Transaction ID: {transactionId ?? "không có"}");
            }
            catch (DbUpdateException dbEx)
            {
                // Chỉ rollback giao dịch nếu nó được tạo mới trong phương thức này
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(dbEx, $"Đã rollback giao dịch do lỗi cập nhật trạng thái thanh toán đơn hàng ID: {id}");
                }

                _logger.LogError(dbEx, $"Lỗi cơ sở dữ liệu khi cập nhật trạng thái thanh toán đơn hàng ID: {id}, Lỗi: {dbEx.Message}");
                throw new InvalidOperationException($"Không thể cập nhật trạng thái thanh toán đơn hàng do lỗi cơ sở dữ liệu: {dbEx.Message}", dbEx);
            }
            catch (Exception ex)
            {
                // Chỉ rollback giao dịch nếu nó được tạo mới trong phương thức này
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Đã rollback giao dịch do lỗi cập nhật trạng thái thanh toán đơn hàng ID: {id}");
                }

                _logger.LogError(ex, $"Lỗi khi cập nhật trạng thái thanh toán đơn hàng ID: {id}, Lỗi: {ex.Message}");
                throw new InvalidOperationException($"Không thể cập nhật trạng thái thanh toán đơn hàng: {ex.Message}", ex);
            }
        }

        // Đối tượng khóa để đồng bộ hóa quá trình hủy đơn hàng
        private static readonly SemaphoreSlim _orderCancellationSemaphore = new(1, 1);

        public async Task<bool> CancelOrderAsync(int id)
        {
            return await CancelOrderAsync(id, null);
        }

        public async Task<bool> CancelOrderAsync(int id, string? userId)
        {
            try
            {
                // Sử dụng khóa để đảm bảo chỉ có một luồng có thể hủy đơn hàng tại một thời điểm
                await _orderCancellationSemaphore.WaitAsync();

                var order = await _context.Orders
                    .IncludeStandardReferences()
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order is null)
                {
                    throw new ArgumentException("Đơn hàng không tồn tại");
                }

                // Nếu userId được cung cấp, kiểm tra xem đơn hàng có thuộc về người dùng không
                if (!string.IsNullOrEmpty(userId) && order.UserId != userId)
                {
                    _logger.LogWarning($"Người dùng {userId} không có quyền hủy đơn hàng ID: {id}");
                    return false;
                }

                // Chỉ cho phép hủy đơn hàng ở trạng thái Pending hoặc Processing
                if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Processing)
                {
                    return false;
                }

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

                    // Cập nhật trạng thái đơn hàng
                    order.Status = OrderStatus.Cancelled;
                    order.UpdatedAt = DateTime.UtcNow;

                    _context.Orders.Update(order);
                    await _context.SaveChangesAsync();

                    // Hoàn lại tồn kho
                    foreach (var item in order.OrderItems)
                    {
                        if (!await _inventoryService.RestockAsync(item.ProductId, item.Quantity))
                        {
                            // Nếu không thể hoàn lại tồn kho, ghi log nhưng vẫn tiếp tục
                            _logger.LogWarning($"Không thể hoàn lại tồn kho cho sản phẩm ID: {item.ProductId}, Tên: {item.ProductName}, Số lượng: {item.Quantity}");
                        }
                    }

                    // Chỉ commit giao dịch nếu chúng ta đã tạo giao dịch mới và không được gọi từ controller
                    // Controller sẽ quản lý giao dịch ở mức cao hơn
                    if (!hasExistingTransaction && transaction != null)
                    {
                        await transaction.CommitAsync();
                        _logger.LogInformation($"Đã hoàn tất giao dịch hủy đơn hàng ID: {id}");
                    }

                    // Gửi email thông báo hủy đơn hàng
                    await _emailService.SendOrderStatusUpdateAsync(id);

                    return true;
                    }
                catch (DbUpdateException dbEx)
                {
                    // Nếu có lỗi cơ sở dữ liệu và chúng ta đã tạo giao dịch mới, hủy giao dịch
                    if (!hasExistingTransaction && transaction != null)
                    {
                        try
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError(dbEx, "Đã hủy giao dịch do lỗi cơ sở dữ liệu khi hủy đơn hàng ID: {OrderId}", id);
                        }
                        catch (Exception rollbackEx)
                        {
                            _logger.LogError(rollbackEx, "Lỗi khi hủy giao dịch cho đơn hàng ID: {OrderId}", id);
                            if (rollbackEx.InnerException != null)
                            {
                                _logger.LogError(rollbackEx.InnerException, "Inner Exception khi hủy giao dịch cho đơn hàng ID: {OrderId}", id);
                            }
                        }
                    }

                    _logger.LogError(dbEx, "Lỗi cơ sở dữ liệu khi hủy đơn hàng ID: {OrderId}", id);
                    return false;
                }
                catch (Exception ex)
                {
                    // Nếu có lỗi và chúng ta đã tạo giao dịch mới, hủy giao dịch
                    if (!hasExistingTransaction && transaction != null)
                    {
                        try
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError(ex, "Đã hủy giao dịch do lỗi khi hủy đơn hàng ID: {OrderId}", id);
                        }
                        catch (Exception rollbackEx)
                        {
                            _logger.LogError(rollbackEx, "Lỗi khi hủy giao dịch cho đơn hàng ID: {OrderId}", id);
                            if (rollbackEx.InnerException != null)
                            {
                                _logger.LogError(rollbackEx.InnerException, "Inner Exception khi hủy giao dịch cho đơn hàng ID: {OrderId}", id);
                            }
                        }
                    }

                    _logger.LogError(ex, "Lỗi khi hủy đơn hàng ID: {OrderId}", id);
                    return false;
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Lỗi cơ sở dữ liệu khi hủy đơn hàng ID: {OrderId}", id);
                if (dbEx.InnerException != null)
                {
                    _logger.LogError(dbEx.InnerException, "Inner Exception khi hủy đơn hàng ID: {OrderId}", id);
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi hủy đơn hàng ID: {OrderId}", id);
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner Exception khi hủy đơn hàng ID: {OrderId}", id);
                }
                return false;
            }
            finally
            {
                // Đảm bảo luôn giải phóng khóa
                _orderCancellationSemaphore.Release();
            }
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
                .GroupBy(o => new { o.UserId, o.CustomerName, o.Email })
                .Select(g => new TopCustomerData
                {
                    UserId = g.Key.UserId ?? string.Empty,
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
            // Kiểm tra xem đã có giao dịch hiện có chưa
            var hasExistingTransaction = _context.Database.CurrentTransaction != null;
            IDbContextTransaction? transaction = null;

            try
            {
                // Chỉ bắt đầu giao dịch mới nếu chưa có giao dịch hiện có
                if (!hasExistingTransaction)
                {
                    transaction = await _context.Database.BeginTransactionAsync();
                    _logger.LogInformation($"Bắt đầu giao dịch mới để cập nhật đơn hàng ID: {order.Id}");
                }
                else
                {
                    _logger.LogInformation($"Sử dụng giao dịch hiện có để cập nhật đơn hàng ID: {order.Id}");
                }

                _context.Entry(order).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                // Chỉ commit giao dịch nếu nó được tạo mới trong phương thức này
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.CommitAsync();
                    _logger.LogInformation($"Đã commit giao dịch cập nhật đơn hàng ID: {order.Id}");
                }
            }
            catch (DbUpdateException dbEx)
            {
                // Chỉ rollback giao dịch nếu nó được tạo mới trong phương thức này
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(dbEx, $"Đã rollback giao dịch do lỗi cập nhật đơn hàng ID: {order.Id}");
                }

                _logger.LogError(dbEx, $"Lỗi cơ sở dữ liệu khi cập nhật đơn hàng ID: {order.Id}, Lỗi: {dbEx.Message}");
                throw new InvalidOperationException($"Không thể cập nhật đơn hàng do lỗi cơ sở dữ liệu: {dbEx.Message}", dbEx);
            }
            catch (Exception ex)
            {
                // Chỉ rollback giao dịch nếu nó được tạo mới trong phương thức này
                if (!hasExistingTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Đã rollback giao dịch do lỗi cập nhật đơn hàng ID: {order.Id}");
                }

                _logger.LogError(ex, $"Lỗi khi cập nhật đơn hàng ID: {order.Id}, Lỗi: {ex.Message}");
                throw new InvalidOperationException($"Không thể cập nhật đơn hàng: {ex.Message}", ex);
            }
        }
        
        public async Task DeleteOrderAsync(int id)
        {
            // Kiểm tra xem đã có giao dịch hiện tại chưa
            var currentTransaction = _context.Database.CurrentTransaction;
            var isNewTransaction = currentTransaction == null;
            IDbContextTransaction? transaction = null;
            
            try
            {
                _logger.LogInformation("Bắt đầu xóa đơn hàng ID: {OrderId}", id);
                
                var order = await _context.Orders.FindAsync(id);
                if (order != null)
                {
                    // Chỉ tạo giao dịch mới nếu chưa có giao dịch hiện tại
                    if (isNewTransaction)
                    {
                        _logger.LogInformation("Bắt đầu giao dịch mới để xóa đơn hàng ID: {OrderId}", id);
                        transaction = await _context.Database.BeginTransactionAsync();
                    }
                    else
                    {
                        _logger.LogInformation("Sử dụng giao dịch hiện có để xóa đơn hàng ID: {OrderId}", id);
                    }
                    
                    try
                    {
                        // Xóa các mục đơn hàng liên quan
                        var orderItems = await _context.OrderItems.Where(oi => oi.OrderId == id).ToListAsync();
                        _logger.LogInformation("Xóa {Count} mục đơn hàng liên quan đến đơn hàng ID: {OrderId}", orderItems.Count, id);
                        _context.OrderItems.RemoveRange(orderItems);
                        
                        // Xóa đơn hàng
                        _logger.LogInformation("Xóa đơn hàng ID: {OrderId}", id);
                        _context.Orders.Remove(order);
                        await _context.SaveChangesAsync();
                        
                        // Hoàn tất giao dịch nếu đây là giao dịch mới
                        if (isNewTransaction && transaction != null)
                        {
                            _logger.LogInformation("Commit giao dịch xóa đơn hàng ID: {OrderId}", id);
                            await transaction.CommitAsync();
                        }
                        
                        _logger.LogInformation("Đã xóa thành công đơn hàng ID: {OrderId}", id);
                    }
                    catch (DbUpdateException dbEx)
                    {
                        // Nếu có lỗi cơ sở dữ liệu và đây là giao dịch mới, hủy giao dịch
                        if (isNewTransaction && transaction != null)
                        {
                            _logger.LogError(dbEx, "Lỗi cơ sở dữ liệu khi xóa đơn hàng ID: {OrderId}, thực hiện rollback giao dịch", id);
                            await transaction.RollbackAsync();
                        }
                        
                        _logger.LogError(dbEx, "Lỗi cơ sở dữ liệu khi xóa đơn hàng ID: {OrderId}, Chi tiết: {Message}", id, dbEx.Message);
                        if (dbEx.InnerException != null)
                        {
                            _logger.LogError(dbEx.InnerException, "Inner Exception khi xóa đơn hàng ID: {OrderId}: {Message}", id, dbEx.InnerException.Message);
                        }
                        
                        throw new InvalidOperationException($"Không thể xóa đơn hàng do lỗi cơ sở dữ liệu: {dbEx.Message}", dbEx);
                    }
                    catch (Exception ex)
                    {
                        // Nếu có lỗi và đây là giao dịch mới, hủy giao dịch
                        if (isNewTransaction && transaction != null)
                        {
                            _logger.LogError(ex, "Lỗi khi xóa đơn hàng ID: {OrderId}, thực hiện rollback giao dịch", id);
                            await transaction.RollbackAsync();
                        }
                        
                        _logger.LogError(ex, "Lỗi khi xóa đơn hàng ID: {OrderId}, Chi tiết: {Message}", id, ex.Message);
                        if (ex.InnerException != null)
                        {
                            _logger.LogError(ex.InnerException, "Inner Exception khi xóa đơn hàng ID: {OrderId}: {Message}", id, ex.InnerException.Message);
                        }
                        
                        throw new InvalidOperationException($"Không thể xóa đơn hàng: {ex.Message}", ex);
                    }
                }
                else
                {
                    _logger.LogWarning("Không tìm thấy đơn hàng ID: {OrderId} để xóa", id);
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Lỗi cơ sở dữ liệu khi xóa đơn hàng ID: {OrderId}, Chi tiết: {Message}", id, dbEx.Message);
                if (dbEx.InnerException != null)
                {
                    _logger.LogError(dbEx.InnerException, "Inner Exception khi xóa đơn hàng ID: {OrderId}: {Message}", id, dbEx.InnerException.Message);
                }
                
                throw new InvalidOperationException($"Không thể xóa đơn hàng do lỗi cơ sở dữ liệu: {dbEx.Message}", dbEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa đơn hàng ID: {OrderId}, Chi tiết: {Message}", id, ex.Message);
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner Exception khi xóa đơn hàng ID: {OrderId}: {Message}", id, ex.InnerException.Message);
                }
                
                throw new InvalidOperationException($"Không thể xóa đơn hàng: {ex.Message}", ex);
            }
        }
    }
}