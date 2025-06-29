using Microsoft.AspNetCore.Mvc;
using CPTStore.Models;
using CPTStore.Services;
using CPTStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using CPTStore.ViewModels;
using CPTStore.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using CPTStore.Data;

namespace CPTStore.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly ICartService _cartService;
        private readonly IPaymentService _paymentService;
        private readonly IEmailService _emailService;
        private readonly IInventoryService _inventoryService;
        private readonly ILogger<OrderController> _logger;
        private readonly ApplicationDbContext _context;

        public OrderController(IOrderService orderService, ICartService cartService, IPaymentService paymentService, IEmailService emailService, IInventoryService inventoryService, ILogger<OrderController> logger, ApplicationDbContext context)
        {
            _orderService = orderService;
            _cartService = cartService;
            _paymentService = paymentService;
            _emailService = emailService;
            _inventoryService = inventoryService;
            _logger = logger;
            _context = context;
        }

        private bool ViewExists(string viewName)
        {
            try
            {
                // Trong ASP.NET Core, cách tốt nhất để kiểm tra view tồn tại là thử render nó
                // Nếu view không tồn tại, sẽ ném ra ngoại lệ
                var viewResult = View(viewName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // GET: /Order/MyOrders
        [AllowAnonymous]
        public async Task<IActionResult> MyOrders(string? status = null, int page = 1, int pageSize = 10)
        {
            string userId = HttpContext.Session.GetUserIdOrSessionId(User);
            var orders = await _orderService.GetUserOrdersAsync(userId);

            // Lọc theo trạng thái nếu có
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, out var orderStatus))
            {
                orders = orders.Where(o => o.Status == orderStatus).ToList();
            }

            // Phân trang
            int totalItems = orders.Count();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            orders = orders.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(orders);
        }

        // GET: /Order/History
        [AllowAnonymous]
        public async Task<IActionResult> History()
        {
            string userId = HttpContext.Session.GetUserIdOrSessionId(User);
            var orders = await _orderService.GetUserOrdersAsync(userId);

            return View(orders);
        }

        // GET: /Order/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            string userId = HttpContext.Session.GetUserIdOrSessionId(User);
            var order = await _orderService.GetOrderAsync(id);

            if (order == null)
            {
                return NotFound();
            }
            
            // Kiểm tra quyền truy cập đơn hàng
            bool hasAccess = false;
            
            // Nếu đơn hàng có UserId và trùng với userId hiện tại
            if (order.UserId != null && order.UserId == userId)
            {
                hasAccess = true;
            }
            // Hoặc nếu đơn hàng có SessionId và trùng với userId hiện tại
            else if (order.SessionId != null && order.SessionId == userId)
            {
                hasAccess = true;
            }
            
            if (!hasAccess)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: /Order/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Create(string customerName, string phoneNumber, string address, string? city, string? postalCode, PaymentMethod paymentMethod, string? notes)
        {
            string userId = HttpContext.Session.GetUserIdOrSessionId(User);
            _logger.LogInformation($"Create action called with userId: {userId}, paymentMethod: {paymentMethod}");

            var cartItems = await _cartService.GetCartItemsAsync(userId);
            if (cartItems.Count == 0)
            {
                _logger.LogWarning("Cart is empty");
                TempData["Error"] = "Giỏ hàng của bạn đang trống";
                return RedirectToAction("Index", "Cart");
            }

            // Bắt đầu giao dịch ở mức controller
            using var transaction = await _context.Database.BeginTransactionAsync();
            _logger.LogInformation($"Đã bắt đầu giao dịch mới ở mức controller cho Create với userId: {userId}");

            try
            {
                _logger.LogInformation($"Creating order for userId: {userId}, customerName: {customerName}");
                var order = await _orderService.CreateOrderAsync(
                    userId, customerName, phoneNumber, address, city, postalCode, paymentMethod, notes);

                _logger.LogInformation($"Order created with id: {order.Id}");

                // Xóa giỏ hàng sau khi tạo đơn hàng
                await _cartService.ClearCartAsync(userId);

                // Kiểm tra trạng thái giao dịch trước khi commit
                try {
                    // Commit giao dịch sau khi tạo đơn hàng thành công
                    await transaction.CommitAsync();
                    _logger.LogInformation($"Đã commit giao dịch ở mức controller cho Create với orderId: {order.Id}");
                } catch (InvalidOperationException ex) when (ex.Message.Contains("has completed")) {
                    // Giao dịch đã được commit trong OrderService, bỏ qua
                    _logger.LogInformation("Giao dịch đã được commit trong OrderService, bỏ qua commit ở controller");
                }

                // Nếu thanh toán online, chuyển hướng đến trang thanh toán
                if (paymentMethod != PaymentMethod.COD)
                {
                    _logger.LogInformation($"Processing online payment for order: {order.Id}");
                    var paymentResult = await _paymentService.ProcessPaymentAsync(
                        order.Id, 
                        paymentMethod.ToString(), 
                        Url.Action("PaymentCallback", "Order", new {}, Request.Scheme));

                    if (paymentResult.Success && !string.IsNullOrEmpty(paymentResult.RedirectUrl))
                    {
                        _logger.LogInformation($"Redirecting to payment gateway: {paymentResult.RedirectUrl}");
                        return Redirect(paymentResult.RedirectUrl);
                    }
                    else if (!string.IsNullOrEmpty(paymentResult.Message))
                    {
                        _logger.LogWarning($"Payment processing failed: {paymentResult.Message}");
                        TempData["Error"] = paymentResult.Message;
                        return RedirectToAction(nameof(Details), new { id = order.Id });
                    }
                }

                // Gửi email xác nhận đơn hàng
                await _orderService.SendOrderConfirmationEmailAsync(order.Id);

                _logger.LogInformation($"Order completed successfully, redirecting to Success page for order: {order.Id}");
                TempData["Success"] = "Đặt hàng thành công!";
                return RedirectToAction(nameof(Success), new { id = order.Id });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("không đủ số lượng trong kho"))
            {
                // Rollback giao dịch nếu có lỗi
                try {
                    await transaction.RollbackAsync();
                    _logger.LogWarning(ex, "Lỗi tồn kho khi đặt hàng: {Message}", ex.Message);
                } catch (InvalidOperationException txEx) when (txEx.Message.Contains("has completed")) {
                    // Giao dịch đã được xử lý trong OrderService
                    _logger.LogInformation("Giao dịch đã được xử lý trong OrderService, bỏ qua rollback ở controller");
                    _logger.LogWarning(ex, "Lỗi gốc tồn kho khi đặt hàng: {Message}", ex.Message);
                }
                
                // Xử lý riêng cho lỗi tồn kho
                string errorMessage = $"{ex.Message}. Bạn có thể thử đồng bộ hóa tồn kho để cập nhật thông tin mới nhất.";
                TempData["Error"] = errorMessage;
                return RedirectToAction("Checkout", "Cart");
            }
            catch (DbUpdateException dbEx)
            {
                // Rollback giao dịch nếu có lỗi
                try {
                    await transaction.RollbackAsync();
                    _logger.LogError(dbEx, "Lỗi cơ sở dữ liệu khi đặt hàng: {Message}", dbEx.Message);
                } catch (InvalidOperationException txEx) when (txEx.Message.Contains("has completed")) {
                    // Giao dịch đã được xử lý trong OrderService
                    _logger.LogInformation("Giao dịch đã được xử lý trong OrderService, bỏ qua rollback ở controller");
                    _logger.LogError(dbEx, "Lỗi gốc cơ sở dữ liệu khi đặt hàng: {Message}", dbEx.Message);
                }
                
                // Thử đồng bộ hóa dữ liệu tồn kho
                try 
                {
                    await _inventoryService.SynchronizeProductStockAsync();
                    _logger.LogInformation("Đã thực hiện đồng bộ hóa tồn kho sau lỗi cập nhật");
                }
                catch (Exception syncEx)
                {
                    _logger.LogError(syncEx, "Không thể đồng bộ hóa tồn kho sau lỗi: {Message}", syncEx.Message);
                }
                
                TempData["Error"] = "Đã xảy ra lỗi khi lưu đơn hàng. Vui lòng thử lại sau.";
                return RedirectToAction("Checkout", "Cart");
            }
            catch (Exception ex)
            {
                // Rollback giao dịch nếu có lỗi
                try {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Lỗi khi đặt hàng: {Message}", ex.Message);
                } catch (InvalidOperationException txEx) when (txEx.Message.Contains("has completed")) {
                    // Giao dịch đã được xử lý trong OrderService
                    _logger.LogInformation("Giao dịch đã được xử lý trong OrderService, bỏ qua rollback ở controller");
                    _logger.LogError(ex, "Lỗi gốc khi đặt hàng: {Message}", ex.Message);
                }
                
                // Hiển thị thông báo lỗi thân thiện với người dùng
                TempData["Error"] = $"Lỗi khi đặt hàng: {ex.Message}";
                return RedirectToAction("Checkout", "Cart");
            }
        }

        // GET: /Order/PaymentCallback
        public async Task<IActionResult> PaymentCallback(string orderId, string transactionId)
        {
            _logger.LogInformation($"PaymentCallback action called with orderId: {orderId}, transactionId: {transactionId}");
            
            if (string.IsNullOrEmpty(orderId) || !int.TryParse(orderId, out int orderIdInt))
            {
                _logger.LogWarning($"Invalid order id: {orderId}");
                return BadRequest("Mã đơn hàng không hợp lệ");
            }

            // Bắt đầu giao dịch ở mức controller
            using var transaction = await _context.Database.BeginTransactionAsync();
            _logger.LogInformation($"Đã bắt đầu giao dịch mới ở mức controller cho PaymentCallback với orderId: {orderIdInt}");
            
            try
            {
                var order = await _orderService.GetOrderAsync(orderIdInt);
                if (order == null)
                {
                    _logger.LogWarning($"Order not found with id: {orderIdInt}");
                    return NotFound("Không tìm thấy đơn hàng");
                }

                _logger.LogInformation($"Verifying payment for order: {orderIdInt}, payment method: {order.PaymentMethod}");
                // Xác minh thanh toán với cổng thanh toán
                var paymentResult = await _paymentService.VerifyPaymentAsync(
                    orderIdInt, order.PaymentMethod.ToString(), transactionId);

                if (paymentResult.Success)
                {
                    _logger.LogInformation($"Payment verification successful for order: {orderIdInt}");
                    // Cập nhật trạng thái thanh toán
                    await _orderService.UpdatePaymentStatusAsync(orderIdInt, PaymentStatus.Completed, transactionId);
                    
                    // Gửi email xác nhận thanh toán
                    await _emailService.SendOrderConfirmationAsync(orderIdInt);
                    
                    // Kiểm tra trạng thái giao dịch trước khi commit
                    try {
                        // Commit giao dịch sau khi hoàn thành tất cả các thao tác
                        await transaction.CommitAsync();
                        _logger.LogInformation($"Đã commit giao dịch ở mức controller cho PaymentCallback với orderId: {orderIdInt}");
                    } catch (InvalidOperationException ex) when (ex.Message.Contains("has completed")) {
                        // Giao dịch đã được commit trong OrderService, bỏ qua
                        _logger.LogInformation("Giao dịch đã được commit trong OrderService, bỏ qua commit ở controller");
                    }
                    
                    _logger.LogInformation($"Payment completed successfully, redirecting to Success page for order: {orderIdInt}");
                    TempData["Success"] = "Thanh toán thành công!";
                    return RedirectToAction(nameof(Success), new { id = orderIdInt });
                }
                else
                {
                    _logger.LogWarning($"Payment verification failed for order: {orderIdInt}, message: {paymentResult.Message}");
                    // Cập nhật trạng thái thanh toán thất bại
                    await _orderService.UpdatePaymentStatusAsync(orderIdInt, PaymentStatus.Failed, transactionId);
                    
                    // Kiểm tra trạng thái giao dịch trước khi commit
                    try {
                        // Commit giao dịch sau khi hoàn thành tất cả các thao tác
                        await transaction.CommitAsync();
                        _logger.LogInformation($"Đã commit giao dịch ở mức controller cho PaymentCallback với orderId: {orderIdInt}");
                    } catch (InvalidOperationException ex) when (ex.Message.Contains("has completed")) {
                        // Giao dịch đã được commit trong OrderService, bỏ qua
                        _logger.LogInformation("Giao dịch đã được commit trong OrderService, bỏ qua commit ở controller");
                    }
                    
                    TempData["Error"] = "Thanh toán thất bại: " + paymentResult.Message;
                    return RedirectToAction(nameof(Details), new { id = orderIdInt });
                }
            }
            catch (Exception ex)
            {
                // Rollback giao dịch nếu có lỗi
                try {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Lỗi khi xử lý PaymentCallback cho đơn hàng ID: {OrderId}, đã rollback giao dịch", orderIdInt);
                } catch (InvalidOperationException txEx) when (txEx.Message.Contains("has completed")) {
                    // Giao dịch đã được xử lý trong OrderService
                    _logger.LogInformation("Giao dịch đã được xử lý trong OrderService, bỏ qua rollback ở controller");
                    _logger.LogError(ex, "Lỗi gốc khi xử lý PaymentCallback cho đơn hàng ID: {OrderId}", orderIdInt);
                }
                
                TempData["Error"] = "Đã xảy ra lỗi khi xử lý thanh toán. Vui lòng liên hệ hỗ trợ.";
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: /Order/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Cancel(int id)
        {
            // Bắt đầu giao dịch ở cấp độ controller
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                _logger.LogInformation("Bắt đầu giao dịch để hủy đơn hàng ID: {OrderId}", id);
                
                string userId = HttpContext.Session.GetUserIdOrSessionId(User);
                var order = await _orderService.GetOrderAsync(id);

                if (order == null)
                {
                    return NotFound();
                }
                
                // Kiểm tra nếu đơn hàng có UserId và khác với userId hiện tại
                if (order.UserId != null && order.UserId != userId)
                {
                    return NotFound();
                }

                if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Processing)
                {
                    TempData["Error"] = "Chỉ có thể hủy đơn hàng ở trạng thái chờ xử lý hoặc đang xử lý";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var success = await _orderService.CancelOrderAsync(id);
                
                if (success)
                {
                    // Kiểm tra trạng thái giao dịch trước khi commit
                    try {
                        // Commit giao dịch nếu mọi thứ thành công
                        await transaction.CommitAsync();
                        _logger.LogInformation("Đã commit giao dịch hủy đơn hàng ID: {OrderId}", id);
                    } catch (InvalidOperationException ex) when (ex.Message.Contains("has completed")) {
                        // Giao dịch đã được commit trong OrderService, bỏ qua
                        _logger.LogInformation("Giao dịch đã được commit trong OrderService, bỏ qua commit ở controller");
                    }
                    
                    TempData["Success"] = "Hủy đơn hàng thành công";
                }
                else
                {
                    // Rollback giao dịch nếu không thành công
                    try {
                        await transaction.RollbackAsync();
                        _logger.LogWarning("Đã rollback giao dịch do không thể hủy đơn hàng ID: {OrderId}", id);
                    } catch (InvalidOperationException ex) when (ex.Message.Contains("has completed")) {
                        // Giao dịch đã được xử lý trong OrderService
                        _logger.LogInformation("Giao dịch đã được xử lý trong OrderService, bỏ qua rollback ở controller");
                    }
                    
                    TempData["Error"] = "Không thể hủy đơn hàng";
                }

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                // Rollback giao dịch nếu có lỗi
                try {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Lỗi khi hủy đơn hàng ID {OrderId}, giao dịch đã được rollback: {Message}", id, ex.Message);
                } catch (InvalidOperationException txEx) when (txEx.Message.Contains("has completed")) {
                    // Giao dịch đã được commit hoặc rollback trong OrderService
                    _logger.LogInformation("Giao dịch đã được xử lý trong OrderService, bỏ qua rollback ở controller");
                    _logger.LogError(ex, "Lỗi gốc khi hủy đơn hàng ID {OrderId}: {Message}", id, ex.Message);
                }
                
                TempData["Error"] = "Đã xảy ra lỗi khi hủy đơn hàng: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // POST: /Order/PlaceOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(CheckoutViewModel model)
        {
            _logger.LogInformation($"PlaceOrder action called with payment method: {model.PaymentMethod}");
            
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid");
                TempData["Error"] = "Vui lòng điền đầy đủ thông tin";
                return RedirectToAction("Checkout", "Cart");
            }

            string userId = HttpContext.Session.GetUserIdOrSessionId(User);
            _logger.LogInformation($"Processing order for userId: {userId}");

            var cartItems = await _cartService.GetCartItemsAsync(userId);
            if (cartItems.Count == 0)
            {
                _logger.LogWarning("Cart is empty");
                TempData["Error"] = "Giỏ hàng của bạn đang trống";
                return RedirectToAction("Index", "Cart");
            }

            try
            {
                // Bắt đầu giao dịch ở mức controller
                using var transaction = await _context.Database.BeginTransactionAsync();
                _logger.LogInformation("Đã bắt đầu giao dịch mới ở mức controller");
                
                var customerName = $"{model.FirstName} {model.LastName}".Trim();
                _logger.LogInformation($"Creating order for customer: {customerName}");
                
                var order = await _orderService.CreateOrderAsync(
                    userId, 
                    customerName, 
                    model.Phone, 
                    model.Address, 
                    model.City, 
                    null, // postalCode not used
                    model.PaymentMethod, 
                    model.Notes);
                
                _logger.LogInformation($"Order created with id: {order.Id}");
                
                // Update district and ward information
                order.District = model.District;
                order.Ward = model.Ward;
                
                // Update shipping fee based on shipping method
                string shippingMethod = Request.Form["ShippingMethod"].ToString();
                _logger.LogInformation($"Shipping method: {shippingMethod}");
                
                if (shippingMethod == "Express")
                {
                    order.ShippingFee = model.ExpressShippingFee;
                }
                else
                {
                    order.ShippingFee = model.StandardShippingFee;
                }
                
                // Lưu thông tin phương thức vận chuyển vào ghi chú
                if (string.IsNullOrEmpty(order.Notes))
                {
                    order.Notes = $"Phương thức vận chuyển: {shippingMethod}";
                }
                else
                {
                    order.Notes += $"\nPhương thức vận chuyển: {shippingMethod}";
                }
                
                await _orderService.UpdateOrderAsync(order);
                _logger.LogInformation($"Order updated with shipping information");
                
                // Kiểm tra trạng thái giao dịch trước khi commit
                try {
                    // Commit giao dịch sau khi hoàn thành tất cả các thao tác
                    await transaction.CommitAsync();
                    _logger.LogInformation("Đã commit giao dịch ở mức controller");
                } catch (InvalidOperationException ex) when (ex.Message.Contains("has completed")) {
                    // Giao dịch đã được commit trong OrderService, bỏ qua
                    _logger.LogInformation("Giao dịch đã được commit trong OrderService, bỏ qua commit ở controller");
                }

                // Xóa giỏ hàng sau khi tạo đơn hàng
                await _cartService.ClearCartAsync(userId);

                // Nếu thanh toán online, chuyển hướng đến trang thanh toán
                if (model.PaymentMethod != PaymentMethod.COD)
                {
                    _logger.LogInformation($"Processing online payment for order: {order.Id}");
                    var paymentResult = await _paymentService.ProcessPaymentAsync(
                        order.Id, 
                        model.PaymentMethod.ToString(), 
                        Url.Action("PaymentCallback", "Order", new {}, Request.Scheme));

                    if (paymentResult.Success && !string.IsNullOrEmpty(paymentResult.RedirectUrl))
                    {
                        _logger.LogInformation($"Redirecting to payment gateway: {paymentResult.RedirectUrl}");
                        return Redirect(paymentResult.RedirectUrl);
                    }
                    else if (!string.IsNullOrEmpty(paymentResult.Message))
                    {
                        _logger.LogWarning($"Payment processing failed: {paymentResult.Message}");
                        TempData["Error"] = paymentResult.Message;
                        return RedirectToAction(nameof(Details), new { id = order.Id });
                    }
                }

                // Gửi email xác nhận đơn hàng
                await _orderService.SendOrderConfirmationEmailAsync(order.Id);

                _logger.LogInformation($"Order completed successfully, redirecting to Success page for order: {order.Id}");
                TempData["Success"] = "Đặt hàng thành công!";
                return RedirectToAction(nameof(Success), new { id = order.Id });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("không đủ số lượng trong kho"))
            {
                // Xử lý riêng cho lỗi tồn kho
                string errorMessage = $"{ex.Message}. Bạn có thể thử đồng bộ hóa tồn kho để cập nhật thông tin mới nhất.";
                TempData["Error"] = errorMessage;
                return RedirectToAction("OutOfStock", "Error", new { message = errorMessage });
            }
            catch (DbUpdateException dbEx)
            {
                // Xử lý riêng cho lỗi cập nhật cơ sở dữ liệu
                _logger.LogError(dbEx, "Lỗi cơ sở dữ liệu khi đặt hàng: {Message}", dbEx.Message);
                
                if (dbEx.InnerException != null)
                {
                    _logger.LogError(dbEx.InnerException, "Inner exception: {Message}", dbEx.InnerException.Message);
                }
                
                // Thử đồng bộ hóa dữ liệu tồn kho
                try 
                {
                    await _inventoryService.SynchronizeProductStockAsync();
                    _logger.LogInformation("Đã thực hiện đồng bộ hóa tồn kho sau lỗi cập nhật");
                }
                catch (Exception syncEx)
                {
                    _logger.LogError(syncEx, "Không thể đồng bộ hóa tồn kho sau lỗi: {Message}", syncEx.Message);
                }
                
                TempData["Error"] = "Đã xảy ra lỗi khi lưu đơn hàng. Vui lòng thử lại sau.";
                return RedirectToAction("Checkout", "Cart");
            }
            catch (Exception ex)
            {
                // Log lỗi
                _logger.LogError(ex, "Lỗi khi đặt hàng: {Message}", ex.Message);
                TempData["Error"] = $"Lỗi khi đặt hàng: {ex.Message}";
                return RedirectToAction("Checkout", "Cart");
            }
        }

        // POST: /Order/CancelOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            string userId = HttpContext.Session.GetUserIdOrSessionId(User);
            var order = await _orderService.GetOrderAsync(orderId);

            if (order == null || order.UserId != userId)
            {
                return NotFound();
            }

            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Processing)
            {
                TempData["Error"] = "Chỉ có thể hủy đơn hàng ở trạng thái chờ xử lý hoặc đang xử lý";
                return RedirectToAction(nameof(MyOrders));
            }

            try
            {
                // Bắt đầu giao dịch ở mức controller
                using var transaction = await _context.Database.BeginTransactionAsync();
                _logger.LogInformation("Bắt đầu giao dịch để hủy đơn hàng ID: {OrderId}", orderId);
                
                var success = await _orderService.CancelOrderAsync(orderId);
                
                if (success)
                {
                    // Kiểm tra trạng thái giao dịch trước khi commit
                    try {
                        // Commit giao dịch nếu mọi thứ thành công
                        await transaction.CommitAsync();
                        _logger.LogInformation("Đã commit giao dịch hủy đơn hàng ID: {OrderId}", orderId);
                    } catch (InvalidOperationException ex) when (ex.Message.Contains("has completed")) {
                        // Giao dịch đã được commit trong OrderService, bỏ qua
                        _logger.LogInformation("Giao dịch đã được commit trong OrderService, bỏ qua commit ở controller");
                    }
                    
                    TempData["Success"] = "Hủy đơn hàng thành công";
                }
                else
                {
                    // Rollback giao dịch nếu không thành công
                    try {
                        await transaction.RollbackAsync();
                        _logger.LogWarning("Đã rollback giao dịch do không thể hủy đơn hàng ID: {OrderId}", orderId);
                    } catch (InvalidOperationException ex) when (ex.Message.Contains("has completed")) {
                        // Giao dịch đã được xử lý trong OrderService
                        _logger.LogInformation("Giao dịch đã được xử lý trong OrderService, bỏ qua rollback ở controller");
                    }
                    
                    TempData["Error"] = "Không thể hủy đơn hàng";
                }
            }
            catch (Exception ex)
            {
                // Rollback giao dịch nếu có lỗi
                if (_context.Database.CurrentTransaction != null)
                {
                    try {
                        await _context.Database.CurrentTransaction.RollbackAsync();
                        _logger.LogError(ex, "Lỗi khi hủy đơn hàng ID {OrderId}, giao dịch đã được rollback: {Message}", orderId, ex.Message);
                    } catch (InvalidOperationException txEx) when (txEx.Message.Contains("has completed")) {
                        // Giao dịch đã được xử lý trong OrderService
                        _logger.LogInformation("Giao dịch đã được xử lý trong OrderService, bỏ qua rollback ở controller");
                        _logger.LogError(ex, "Lỗi gốc khi hủy đơn hàng ID {OrderId}: {Message}", orderId, ex.Message);
                    }
                }
                else
                {
                    _logger.LogError(ex, "Lỗi khi hủy đơn hàng ID {OrderId}: {Message}", orderId, ex.Message);
                }
                
                TempData["Error"] = $"Lỗi khi hủy đơn hàng: {ex.Message}";
            }

            return RedirectToAction(nameof(MyOrders));
        }

        // GET: /Order/Invoice/5
        public async Task<IActionResult> Invoice(int id)
        {
            string userId = HttpContext.Session.GetUserIdOrSessionId(User);
            _logger.LogInformation("Invoice action called for order ID: {OrderId}, userId: {UserId}", id, userId);
            
            // Bắt đầu giao dịch ở mức controller (chỉ đọc)
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            _logger.LogInformation("Đã bắt đầu giao dịch chỉ đọc ở mức controller cho Invoice với orderId: {OrderId}", id);
            
            try
            {
                var order = await _orderService.GetOrderAsync(id);

                if (order == null || order.UserId != userId)
                {
                    _logger.LogWarning("Order not found or belongs to different user. Order ID: {OrderId}, current userId: {UserId}", id, userId);
                    return NotFound();
                }

                var pdfBytes = await _orderService.GenerateInvoicePdfAsync(id);
                
                // Kiểm tra trạng thái giao dịch trước khi commit
                try {
                    // Commit giao dịch sau khi hoàn thành tất cả các thao tác
                    await transaction.CommitAsync();
                    _logger.LogInformation("Đã commit giao dịch ở mức controller cho Invoice với orderId: {OrderId}", id);
                } catch (InvalidOperationException ex) when (ex.Message.Contains("has completed")) {
                    // Giao dịch đã được commit trong OrderService, bỏ qua
                    _logger.LogInformation("Giao dịch đã được commit trong OrderService, bỏ qua commit ở controller");
                }
                
                return File(pdfBytes, "application/pdf", $"invoice_{order.OrderNumber}.pdf");
            }
            catch (Exception ex)
            {
                // Rollback giao dịch nếu có lỗi
                try {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Lỗi khi tạo hóa đơn cho đơn hàng ID: {OrderId}, đã rollback giao dịch", id);
                } catch (InvalidOperationException txEx) when (txEx.Message.Contains("has completed")) {
                    // Giao dịch đã được xử lý trong OrderService
                    _logger.LogInformation("Giao dịch đã được xử lý trong OrderService, bỏ qua rollback ở controller");
                    _logger.LogError(ex, "Lỗi gốc khi tạo hóa đơn cho đơn hàng ID: {OrderId}", id);
                }
                
                TempData["Error"] = "Đã xảy ra lỗi khi tạo hóa đơn. Vui lòng thử lại sau.";
                return RedirectToAction(nameof(Details), new { id = id });
            }
        }

        // GET: /Order/Success/5
        [AllowAnonymous]
        public async Task<IActionResult> Success(int id)
        {
            string userId = HttpContext.Session.GetUserIdOrSessionId(User);
            _logger.LogInformation("Success action called with id: {OrderId}, userId: {UserId}", id, userId);
            
            // Bắt đầu giao dịch ở mức controller (chỉ đọc)
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            _logger.LogInformation("Đã bắt đầu giao dịch chỉ đọc ở mức controller cho Success với orderId: {OrderId}", id);
            
            try
            {
                var order = await _orderService.GetOrderAsync(id);

                if (order == null)
                {
                    _logger.LogWarning("Order not found with id: {OrderId}", id);
                    return NotFound();
                }
                
                // Kiểm tra nếu đơn hàng có UserId và khác với userId hiện tại
                if (order.UserId != null && order.UserId != userId)
                {
                    _logger.LogWarning("Order belongs to different user. Order.UserId: {OrderUserId}, current userId: {UserId}", order.UserId, userId);
                    return NotFound();
                }

                _logger.LogInformation("Returning Success view for order: {OrderId}", id);
                
                // Kiểm tra trạng thái giao dịch trước khi commit
                try {
                    // Commit giao dịch sau khi hoàn thành tất cả các thao tác
                    await transaction.CommitAsync();
                    _logger.LogInformation("Đã commit giao dịch ở mức controller cho Success với orderId: {OrderId}", id);
                } catch (InvalidOperationException ex) when (ex.Message.Contains("has completed")) {
                    // Giao dịch đã được commit trong OrderService, bỏ qua
                    _logger.LogInformation("Giao dịch đã được commit trong OrderService, bỏ qua commit ở controller");
                }
                
                // Kiểm tra xem view có tồn tại không
                if (ViewExists("Success"))
                {
                    _logger.LogInformation("Success view exists, returning view for order: {OrderId}", id);
                    return View("Success", order);
                }
                else
                {
                    _logger.LogWarning("Success view does not exist, redirecting to Details for order: {OrderId}", id);
                    return RedirectToAction(nameof(Details), new { id = id });
                }
            }
            catch (Exception ex)
            {
                // Rollback giao dịch nếu có lỗi
                try {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Lỗi khi hiển thị trang Success cho đơn hàng ID: {OrderId}, đã rollback giao dịch", id);
                } catch (InvalidOperationException txEx) when (txEx.Message.Contains("has completed")) {
                    // Giao dịch đã được xử lý trong OrderService
                    _logger.LogInformation("Giao dịch đã được xử lý trong OrderService, bỏ qua rollback ở controller");
                    _logger.LogError(ex, "Lỗi gốc khi hiển thị trang Success cho đơn hàng ID: {OrderId}", id);
                }
                
                // Fallback to Details view if Success view fails
                return RedirectToAction(nameof(Details), new { id = id });
            }
        }
    }
}