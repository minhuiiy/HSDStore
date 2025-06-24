using Microsoft.AspNetCore.Mvc;
using CPTStore.Models;
using CPTStore.Services;
using CPTStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace CPTStore.Controllers
{
    public class CartController : Controller
    {
        private readonly ICartService _cartService;
        private readonly IProductService _productService;
        private readonly IDiscountService _discountService;

        public CartController(
            ICartService cartService,
            IProductService productService,
            IDiscountService discountService)
        {
            _cartService = cartService;
            _productService = productService;
            _discountService = discountService;
        }

        // GET: /Cart
        public async Task<IActionResult> Index()
        {
            string userId = User.Identity?.IsAuthenticated == true 
                ? User.FindFirst("sub")?.Value ?? HttpContext.Session.Id
                : HttpContext.Session.Id;

            var cartItems = await _cartService.GetCartItemsAsync(userId);
            var cartTotal = await _cartService.GetCartTotalAsync(userId);

            ViewBag.CartTotal = cartTotal;
            ViewBag.RecommendedProducts = await _productService.GetFeaturedProductsAsync(4);

            return View(cartItems);
        }

        // POST: /Cart/AddToCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            if (quantity < 1)
            {
                return BadRequest("Số lượng phải lớn hơn 0");
            }

            string userId = User.Identity?.IsAuthenticated == true 
                ? User.FindFirst("sub")?.Value ?? HttpContext.Session.Id
                : HttpContext.Session.Id;

            await _cartService.AddToCartAsync(userId, productId, quantity);

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/UpdateQuantity
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int id, int quantity)
        {
            if (quantity < 1)
            {
                return BadRequest("Số lượng phải lớn hơn 0");
            }

            await _cartService.UpdateCartItemAsync(id, quantity);

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/RemoveItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveItem(int id)
        {
            await _cartService.RemoveFromCartAsync(id);

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/ClearCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearCart()
        {
            string userId = User.Identity?.IsAuthenticated == true 
                ? User.FindFirst("sub")?.Value ?? HttpContext.Session.Id
                : HttpContext.Session.Id;

            await _cartService.ClearCartAsync(userId);

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

            string userId = User.Identity?.IsAuthenticated == true 
                ? User.FindFirst("sub")?.Value ?? HttpContext.Session.Id
                : HttpContext.Session.Id;

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

        // POST: /Cart/RemoveDiscount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveDiscount()
        {
            string userId = User.Identity?.IsAuthenticated == true 
                ? User.FindFirst("sub")?.Value ?? HttpContext.Session.Id
                : HttpContext.Session.Id;

            await _cartService.RemoveDiscountAsync(userId);
            TempData["Success"] = "Đã xóa mã giảm giá";

            return RedirectToAction(nameof(Index));
        }

        // GET: /Cart/Checkout
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            string userId = User.FindFirst("sub")?.Value ?? "";

            var cartItems = await _cartService.GetCartItemsAsync(userId);
            if (!cartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống";
                return RedirectToAction(nameof(Index));
            }

            var cartTotal = await _cartService.GetCartTotalAsync(userId);
            ViewBag.CartTotal = cartTotal;
            ViewBag.CartItems = cartItems;

            return View();
        }
    }
}