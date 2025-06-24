using Microsoft.AspNetCore.Mvc;
using CPTStore.Models;
using CPTStore.Services;
using CPTStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace CPTStore.Controllers
{
    [Authorize]
    public class OrderController(IOrderService orderService, ICartService cartService, IPaymentService paymentService, IEmailService emailService) : Controller
    {
        private readonly IOrderService _orderService = orderService;
        private readonly ICartService _cartService = cartService;
        private readonly IPaymentService _paymentService = paymentService;
        private readonly IEmailService _emailService = emailService;

        // GET: /Order/MyOrders
        public async Task<IActionResult> MyOrders(string? status = null, int page = 1, int pageSize = 10)
        {
            string userId = User.FindFirst("sub")?.Value ?? "";
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
        public async Task<IActionResult> History()
        {
            string userId = User.FindFirst("sub")?.Value ?? "";
            var orders = await _orderService.GetUserOrdersAsync(userId);

            return View(orders);
        }

        // GET: /Order/Details/5
        public async Task<IActionResult> Details(int id)
        {
            string userId = User.FindFirst("sub")?.Value ?? "";
            var order = await _orderService.GetOrderAsync(id);

            if (order == null || order.UserId != userId)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: /Order/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string customerName, string phoneNumber, string address, string? city, string? postalCode, PaymentMethod paymentMethod, string? notes)
        {
            string userId = User.FindFirst("sub")?.Value ?? "";

            var cartItems = await _cartService.GetCartItemsAsync(userId);
            if (cartItems.Count == 0)
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống";
                return RedirectToAction("Index", "Cart");
            }

            try
            {
                var order = await _orderService.CreateOrderAsync(
                    userId, customerName, phoneNumber, address, city, postalCode, paymentMethod, notes);

                // Xóa giỏ hàng sau khi tạo đơn hàng
                await _cartService.ClearCartAsync(userId);

                // Nếu thanh toán online, chuyển hướng đến trang thanh toán
                if (paymentMethod != PaymentMethod.COD)
                {
                    var paymentResult = await _paymentService.ProcessPaymentAsync(
                        order.Id, 
                        paymentMethod.ToString(), 
                        Url.Action("PaymentCallback", "Order", new {}, Request.Scheme));

                    if (paymentResult.Success && !string.IsNullOrEmpty(paymentResult.RedirectUrl))
                    {
                        return Redirect(paymentResult.RedirectUrl);
                    }
                    else if (!string.IsNullOrEmpty(paymentResult.Message))
                    {
                        TempData["Error"] = paymentResult.Message;
                        return RedirectToAction(nameof(Details), new { id = order.Id });
                    }
                }

                // Gửi email xác nhận đơn hàng
                await _orderService.SendOrderConfirmationEmailAsync(order.Id);

                TempData["Success"] = "Đặt hàng thành công!";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi đặt hàng: {ex.Message}";
                return RedirectToAction("Checkout", "Cart");
            }
        }

        // GET: /Order/PaymentCallback
        public async Task<IActionResult> PaymentCallback(string orderId, string transactionId)
        {
            if (string.IsNullOrEmpty(orderId) || !int.TryParse(orderId, out int orderIdInt))
            {
                return BadRequest("Mã đơn hàng không hợp lệ");
            }

            var order = await _orderService.GetOrderAsync(orderIdInt);
            if (order == null)
            {
                return NotFound("Không tìm thấy đơn hàng");
            }

            // Xác minh thanh toán với cổng thanh toán
            var paymentResult = await _paymentService.VerifyPaymentAsync(
                orderIdInt, order.PaymentMethod.ToString(), transactionId);

            if (paymentResult.Success)
            {
                // Cập nhật trạng thái thanh toán
                await _orderService.UpdatePaymentStatusAsync(orderIdInt, PaymentStatus.Completed, transactionId);
                
                // Gửi email xác nhận thanh toán
                await _emailService.SendOrderConfirmationAsync(orderIdInt);
                
                TempData["Success"] = "Thanh toán thành công!";
            }
            else
            {
                // Cập nhật trạng thái thanh toán thất bại
                await _orderService.UpdatePaymentStatusAsync(orderIdInt, PaymentStatus.Failed, transactionId);
                TempData["Error"] = "Thanh toán thất bại: " + paymentResult.Message;
            }

            return RedirectToAction(nameof(Details), new { id = orderIdInt });
        }

        // POST: /Order/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            string userId = User.FindFirst("sub")?.Value ?? "";
            var order = await _orderService.GetOrderAsync(id);

            if (order == null || order.UserId != userId)
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
                TempData["Success"] = "Hủy đơn hàng thành công";
            }
            else
            {
                TempData["Error"] = "Không thể hủy đơn hàng";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Order/CancelOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            string userId = User.FindFirst("sub")?.Value ?? "";
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

            var success = await _orderService.CancelOrderAsync(orderId);
            if (success)
            {
                TempData["Success"] = "Hủy đơn hàng thành công";
            }
            else
            {
                TempData["Error"] = "Không thể hủy đơn hàng";
            }

            return RedirectToAction(nameof(MyOrders));
        }

        // GET: /Order/Invoice/5
        public async Task<IActionResult> Invoice(int id)
        {
            string userId = User.FindFirst("sub")?.Value ?? "";
            var order = await _orderService.GetOrderAsync(id);

            if (order == null || order.UserId != userId)
            {
                return NotFound();
            }

            var pdfBytes = await _orderService.GenerateInvoicePdfAsync(id);
            return File(pdfBytes, "application/pdf", $"invoice_{order.OrderNumber}.pdf");
        }
    }
}