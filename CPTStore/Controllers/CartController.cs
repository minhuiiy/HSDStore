using Microsoft.AspNetCore.Mvc;
using CPTStore.Models;
using CPTStore.Services;
using CPTStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using CPTStore.ViewModels;
using CPTStore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using CPTStore.Extensions;
using Microsoft.Extensions.Logging;

namespace CPTStore.Controllers
{
    public class CartController(ICartService cartService, IProductService productService, IDiscountService discountService, ISavedDiscountService savedDiscountService, IInventoryService inventoryService, ApplicationDbContext context, ILogger<CartController> logger) : Controller
    {
        private readonly ICartService _cartService = cartService;
        private readonly IProductService _productService = productService;
        private readonly IDiscountService _discountService = discountService;
        private readonly ISavedDiscountService _savedDiscountService = savedDiscountService;
        private readonly IInventoryService _inventoryService = inventoryService;
        private readonly ApplicationDbContext _context = context;
        private readonly ILogger<CartController> _logger = logger;

        // GET: /Cart
        public async Task<IActionResult> Index()
        {
            string userId = HttpContext.Session.GetUserIdOrSessionId(User);

            var cart = await _cartService.GetCartAsync(userId);
            ViewBag.RecommendedProducts = await _productService.GetFeaturedProductsAsync(4);

            return View(cart);
        }

        // GET: /Cart/AddToCart
        [HttpGet]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1, string? returnUrl = null)
        {
            if (quantity < 1)
            {
                TempData["Error"] = "Số lượng phải lớn hơn 0";
                return RedirectToAction(nameof(Index));
            }

            string userId = HttpContext.Session.GetUserIdOrSessionId(User);

            try
            {
                // Kiểm tra tồn kho trước khi thêm vào giỏ hàng
                bool isInStock = await _inventoryService.IsInStockAsync(productId, quantity);
                if (!isInStock)
                {
                    var product = await _productService.GetProductByIdAsync(productId);
                    TempData["Error"] = $"Sản phẩm '{product?.Name ?? "đã chọn"}' không đủ số lượng trong kho. Bạn có thể thử <a href='{Url.Action("SynchronizeStock", "Inventory")}'>đồng bộ hóa tồn kho</a> để cập nhật thông tin mới nhất.";
                    return !string.IsNullOrEmpty(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
                }
                
                var result = await _cartService.AddToCartAsync(userId, productId, quantity);
                if (result > 0)
                {
                    TempData["Success"] = "Sản phẩm đã được thêm vào giỏ hàng";
                }
                else
                {
                    // Kiểm tra lý do không thể thêm vào giỏ hàng
                    var product = await _productService.GetProductByIdAsync(productId);
                    if (product != null && !product.IsAvailable)
                    {
                        TempData["Error"] = $"Sản phẩm '{product.Name}' hiện không có sẵn để mua";
                    }
                    else
                    {
                        TempData["Error"] = "Không thể thêm sản phẩm vào giỏ hàng. Vui lòng thử lại sau";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm sản phẩm vào giỏ hàng");
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            // Nếu có returnUrl, chuyển hướng về trang đó
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }
            
            return RedirectToAction(nameof(Index));
        }
        
        // POST: /Cart/AddToCartPost
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCartPost(int productId, int quantity = 1, string? returnUrl = null)
        {
            if (quantity < 1)
            {
                TempData["Error"] = "Số lượng phải lớn hơn 0";
                return RedirectToAction(nameof(Index));
            }

            string userId = HttpContext.Session.GetUserIdOrSessionId(User);

            try
            {
                // Kiểm tra tồn kho trước khi thêm vào giỏ hàng
                bool isInStock = await _inventoryService.IsInStockAsync(productId, quantity);
                if (!isInStock)
                {
                    var product = await _productService.GetProductByIdAsync(productId);
                    TempData["Error"] = $"Sản phẩm '{product?.Name ?? "đã chọn"}' không đủ số lượng trong kho. Bạn có thể thử <a href='{Url.Action("SynchronizeStock", "Inventory")}'>đồng bộ hóa tồn kho</a> để cập nhật thông tin mới nhất.";
                    return !string.IsNullOrEmpty(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
                }
                
                var result = await _cartService.AddToCartAsync(userId, productId, quantity);
                if (result > 0)
                {
                    TempData["Success"] = "Sản phẩm đã được thêm vào giỏ hàng";
                }
                else
                {
                    // Kiểm tra lý do không thể thêm vào giỏ hàng
                    var product = await _productService.GetProductByIdAsync(productId);
                    if (product != null && !product.IsAvailable)
                    {
                        TempData["Error"] = $"Sản phẩm '{product.Name}' hiện không có sẵn để mua";
                    }
                    else
                    {
                        TempData["Error"] = "Không thể thêm sản phẩm vào giỏ hàng. Vui lòng thử lại sau";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm sản phẩm vào giỏ hàng");
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            // Nếu có returnUrl, chuyển hướng về trang đó
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }
            
            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/UpdateQuantity
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, string action)
        {
            var cartItem = await _cartService.GetCartItemAsync(cartItemId);
            if (cartItem == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm trong giỏ hàng";
                return RedirectToAction(nameof(Index));
            }
            
            int newQuantity = cartItem.Quantity;
            if (action == "increase")
            {
                newQuantity += 1;
            }
            else if (action == "decrease")
            {
                newQuantity = Math.Max(1, cartItem.Quantity - 1);
            }
            
            try
            {
                // Kiểm tra tồn kho trước khi cập nhật số lượng
                if (action == "increase" || newQuantity > cartItem.Quantity)
                {
                    bool isInStock = await _inventoryService.IsInStockAsync(cartItem.ProductId, newQuantity);
                    if (!isInStock)
                    {
                        TempData["Error"] = $"Sản phẩm '{cartItem.ProductName}' không đủ số lượng trong kho. Bạn có thể thử <a href='{Url.Action("SynchronizeStock", "Inventory")}'>đồng bộ hóa tồn kho</a> để cập nhật thông tin mới nhất.";
                        return RedirectToAction(nameof(Index));
                    }
                }
                
                await _cartService.UpdateCartItemAsync(cartItemId, newQuantity);
                TempData["Success"] = "Số lượng sản phẩm đã được cập nhật";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật số lượng sản phẩm");
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
        
        // POST: /Cart/UpdateCartItem (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCartItem([FromBody] UpdateCartItemRequest request)
        {
            if (request.Quantity < 1)
            {
                return Json(new { success = false, message = "Số lượng phải lớn hơn 0" });
            }
            
            var cartItem = await _cartService.GetCartItemAsync(request.CartItemId);
            if (cartItem == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm trong giỏ hàng" });
            }
            
            try
            {
                // Kiểm tra tồn kho trước khi cập nhật số lượng
                if (request.Quantity > cartItem.Quantity)
                {
                    bool isInStock = await _inventoryService.IsInStockAsync(cartItem.ProductId, request.Quantity);
                    if (!isInStock)
                    {
                        return Json(new { 
                            success = false, 
                            message = $"Sản phẩm '{cartItem.ProductName}' không đủ số lượng trong kho. Bạn có thể thử đồng bộ hóa tồn kho để cập nhật thông tin mới nhất." 
                        });
                    }
                }
                
                await _cartService.UpdateCartItemAsync(request.CartItemId, request.Quantity);
                
                // Lấy thông tin giỏ hàng đã cập nhật
                string userId = HttpContext.Session.GetUserIdOrSessionId(User);
                var cart = await _cartService.GetCartAsync(userId);
                
                // Tính lại giá tiền của sản phẩm đã cập nhật
                var updatedCartItem = cart.CartItems.FirstOrDefault(ci => ci.Id == request.CartItemId);
                var itemTotal = updatedCartItem != null ? updatedCartItem.Quantity * updatedCartItem.UnitPrice : 0;
                
                return Json(new { 
                    success = true, 
                    message = "Số lượng sản phẩm đã được cập nhật",
                    itemTotal = $"{itemTotal:N0} VNĐ",
                    discount = $"{cart.Discount:N0} VNĐ",
                    subtotal = $"{cart.TotalAmount:N0} VNĐ",
                    total = $"{cart.TotalAmount + cart.ShippingFee - cart.Discount:N0} VNĐ",
                    shippingFee = $"{cart.ShippingFee:N0} VNĐ",
                    itemCount = cart.CartItems?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật số lượng sản phẩm");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Cart/RemoveItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveItem(int id)
        {
            try
            {
                await _cartService.RemoveFromCartAsync(id);
                TempData["Success"] = "Sản phẩm đã được xóa khỏi giỏ hàng";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa sản phẩm khỏi giỏ hàng");
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
        
        // POST: /Cart/RemoveCartItem (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCartItem([FromBody] CartItemRequest request)
        {
            try
            {
                await _cartService.RemoveFromCartAsync(request.CartItemId);
                
                // Lấy thông tin giỏ hàng đã cập nhật
                string userId = HttpContext.Session.GetUserIdOrSessionId(User);
                var cart = await _cartService.GetCartAsync(userId);
                
                return Json(new { 
                    success = true, 
                    message = "Sản phẩm đã được xóa khỏi giỏ hàng",
                    discount = $"{cart.Discount:N0} VNĐ",
                    subtotal = $"{cart.TotalAmount:N0} VNĐ",
                    total = $"{cart.TotalAmount + cart.ShippingFee - cart.Discount:N0} VNĐ",
                    shippingFee = $"{cart.ShippingFee:N0} VNĐ",
                    itemCount = cart.CartItems?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa sản phẩm khỏi giỏ hàng");
                return Json(new { success = false, message = ex.Message });
            }
        }
        
        // POST: /Cart/RemoveFromCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCart(int cartItemId)
        {
            try
            {
                await _cartService.RemoveFromCartAsync(cartItemId);
                TempData["Success"] = "Sản phẩm đã được xóa khỏi giỏ hàng";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa sản phẩm khỏi giỏ hàng");
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/ClearCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearCart()
        {
            string userId = HttpContext.Session.GetUserIdOrSessionId(User);

            try
            {
                await _cartService.ClearCartAsync(userId);
                TempData["Success"] = "Giỏ hàng đã được xóa";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa giỏ hàng");
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/ApplyDiscount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyDiscount(string discountCode)
        {
            if (string.IsNullOrWhiteSpace(discountCode))
            {
                TempData["Error"] = "Vui lòng nhập mã giảm giá";
                return RedirectToAction(nameof(Index));
            }

            string userId = HttpContext.Session.GetUserIdOrSessionId(User);

            try
            {
                var discountAmount = await _cartService.ApplyDiscountAsync(userId, discountCode);
                TempData["Success"] = $"Áp dụng mã giảm giá thành công. Bạn được giảm {discountAmount:N0} VNĐ";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
        
        // POST: /Cart/ApplyDiscountAjax
        [HttpPost]
        [Route("Cart/ApplyDiscountAjax")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyDiscountAjax([FromBody] DiscountRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DiscountCode))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã giảm giá" });
            }

            string userId = HttpContext.Session.GetUserIdOrSessionId(User);

            try
            {
                var discountAmount = await _cartService.ApplyDiscountAsync(userId, request.DiscountCode);
                
                // Lấy thông tin giỏ hàng đã cập nhật
                var cart = await _cartService.GetCartAsync(userId);
                
                return Json(new { 
                    success = true, 
                    message = $"Áp dụng mã giảm giá thành công. Bạn được giảm {discountAmount:N0} VNĐ",
                    discount = $"{cart.Discount:N0} VNĐ",
                    subtotal = $"{cart.TotalAmount:N0} VNĐ",
                    total = $"{cart.TotalAmount + cart.ShippingFee - cart.Discount:N0} VNĐ",
                    shippingFee = $"{cart.ShippingFee:N0} VNĐ",
                    itemCount = cart.CartItems?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Cart/RemoveDiscount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveDiscount()
        {
            string userId = HttpContext.Session.GetUserIdOrSessionId(User);

            await _cartService.RemoveDiscountAsync(userId);
            TempData["Success"] = "Đã xóa mã giảm giá";

            return RedirectToAction(nameof(Index));
        }

        // GET: /Cart/Checkout
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            string userId = HttpContext.Session.GetUserIdOrSessionId(User);

            var cart = await _cartService.GetCartAsync(userId);
            if (cart.CartItems == null || cart.CartItems.Count == 0)
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống";
                return RedirectToAction(nameof(Index));
            }
            
            // Kiểm tra tồn kho trước khi cho phép thanh toán
            try
            {
                foreach (var item in cart.CartItems)
                {
                    bool isInStock = await _inventoryService.IsInStockAsync(item.ProductId, item.Quantity);
                    if (!isInStock)
                    {
                        TempData["Error"] = $"Sản phẩm '{item.ProductName}' không đủ số lượng trong kho. Bạn có thể thử <a href='{Url.Action("SynchronizeStock", "Inventory")}'>đồng bộ hóa tồn kho</a> để cập nhật thông tin mới nhất.";
                        return RedirectToAction(nameof(Index));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra tồn kho");
                TempData["Error"] = "Có lỗi xảy ra khi kiểm tra tồn kho. Vui lòng thử lại sau.";
                return RedirectToAction(nameof(Index));
            }

            // Tạo CheckoutViewModel từ Cart
            var checkoutViewModel = new CheckoutViewModel
            {
                CartItems = cart.CartItems.ToList(),
                SubTotal = cart.TotalAmount,
                ShippingFee = cart.ShippingFee,
                Discount = cart.Discount,
                TotalAmount = cart.TotalAmount + cart.ShippingFee - cart.Discount
            };
            
            // Tính phí vận chuyển dựa trên tổng giá trị đơn hàng
            if (cart.TotalAmount > 500000)
            {
                checkoutViewModel.StandardShippingFee = 0; // Miễn phí vận chuyển tiêu chuẩn cho đơn hàng > 500k
                checkoutViewModel.ExpressShippingFee = 30000; // Giảm phí vận chuyển nhanh cho đơn hàng > 500k
            }
            else
            {
                checkoutViewModel.StandardShippingFee = 30000;
                checkoutViewModel.ExpressShippingFee = 50000;
            }

            // Nếu người dùng đã đăng nhập, điền thông tin từ tài khoản
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null)
                {
                    checkoutViewModel.FirstName = user.FirstName ?? string.Empty;
                    checkoutViewModel.LastName = user.LastName ?? string.Empty;
                    checkoutViewModel.Email = user.Email ?? string.Empty;
                    checkoutViewModel.Phone = user.PhoneNumber ?? string.Empty;
                    checkoutViewModel.Address = user.Address ?? string.Empty;
                    checkoutViewModel.City = user.City ?? string.Empty;
                }
            }

            return View(checkoutViewModel);
        }
        
        // GET: /Cart/GetSavedDiscounts
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetSavedDiscounts()
        {
            string userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập để xem mã giảm giá đã lưu" });
            }

            try
            {
                var savedDiscounts = await _savedDiscountService.GetSavedDiscountsByUserIdAsync(userId);
                var validDiscounts = savedDiscounts.Where(sd => !sd.IsUsed && sd.Discount != null && sd.Discount.IsActive && sd.Discount.EndDate > DateTime.UtcNow);
                
                var result = validDiscounts.Select(sd => new
                {
                    id = sd.Id,
                    code = sd.DiscountCode,
                    description = sd.Discount?.Description,
                    discountAmount = sd.Discount?.DiscountType == DiscountType.FixedAmount ? sd.Discount?.Value : null,
                    discountPercentage = sd.Discount?.DiscountType == DiscountType.Percentage ? sd.Discount?.Value : null,
                    expiryDate = sd.Discount?.EndDate
                });

                return Json(new { success = true, discounts = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Cart/GetShippingFee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetShippingFee([FromBody] ShippingMethodRequest request)
        {
            if (string.IsNullOrEmpty(request.ShippingMethod))
            {
                return Json(new { success = false, message = "Phương thức vận chuyển không hợp lệ" });
            }
            
            string userId = HttpContext.Session.GetUserIdOrSessionId(User);
            var cart = await _cartService.GetCartAsync(userId);
            
            if (request.ShippingMethod == "Express")
            {
                // Phí vận chuyển nhanh (thường cao hơn tiêu chuẩn)
                var shippingFee = cart.TotalAmount > 500000 ? 30000 : 50000;
            }
            else
            {
                // Phí vận chuyển tiêu chuẩn
                var shippingFee = cart.TotalAmount > 500000 ? 0 : 30000;
            }
            
            // Calculate shipping fee again based on the method
            decimal calculatedShippingFee = request.ShippingMethod == "Express" ?
                (cart.TotalAmount > 500000 ? 30000 : 50000) :
                (cart.TotalAmount > 500000 ? 0 : 30000);
            
            decimal total = cart.TotalAmount + calculatedShippingFee - cart.Discount;
            
            return Json(new { 
                success = true, 
                shippingFee = $"{calculatedShippingFee:N0} VNĐ", 
                total = $"{total:N0} VNĐ" 
            });
        }
        
        // POST: /Cart/GetPaymentFee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetPaymentFee([FromBody] PaymentMethodRequest request)
        {
            if (string.IsNullOrEmpty(request.PaymentMethod))
            {
                return Json(new { success = false, message = "Phương thức thanh toán không hợp lệ" });
            }
            
            string userId = HttpContext.Session.GetUserIdOrSessionId(User);
            var cart = await _cartService.GetCartAsync(userId);
            
            decimal paymentFee = 0;
            // Tính phí xử lý thanh toán dựa trên phương thức thanh toán
            if (request.PaymentMethod == "CreditCard")
            {
                // Phí xử lý thẻ tín dụng (thường là % của tổng đơn hàng)
                paymentFee = Math.Round(cart.TotalAmount * 0.02m, 0);
            }
            else if (request.PaymentMethod == "BankTransfer")
            {
                // Phí chuyển khoản ngân hàng (có thể cố định)
                paymentFee = 10000;
            }
            
            decimal total = cart.TotalAmount + cart.ShippingFee + paymentFee - cart.Discount;
            
            return Json(new { 
                success = true, 
                paymentFee = $"{paymentFee:N0} VNĐ", 
                total = $"{total:N0} VNĐ" 
            });
        }
    }
    
    public class ShippingMethodRequest
    {
        public string ShippingMethod { get; set; } = "";
    }
    
    public class PaymentMethodRequest
    {
        public string PaymentMethod { get; set; } = "";
    }
    
    public class DiscountRequest
    {
        public string DiscountCode { get; set; } = "";
    }
    
    public class CartItemRequest
    {
        public int CartItemId { get; set; }
    }
    
    public class UpdateCartItemRequest
    {
        public int CartItemId { get; set; }
        public int Quantity { get; set; }
    }
}