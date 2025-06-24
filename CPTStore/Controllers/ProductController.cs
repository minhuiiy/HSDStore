using Microsoft.AspNetCore.Mvc;
using CPTStore.Models;
using CPTStore.Services;
using CPTStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace CPTStore.Controllers
{
    public class ProductController : Controller
    {
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly ICartService _cartService;
        private readonly IRecommendationService _recommendationService;

        public ProductController(
            IProductService productService,
            ICategoryService categoryService,
            ICartService cartService,
            IRecommendationService recommendationService)
        {
            _productService = productService;
            _categoryService = categoryService;
            _cartService = cartService;
            _recommendationService = recommendationService;
        }

        // GET: /Product
        public async Task<IActionResult> Index(int? categoryId, string searchTerm, int page = 1)
        {
            const int pageSize = 12;
            IEnumerable<Product> products;

            if (!string.IsNullOrEmpty(searchTerm))
            {
                products = await _productService.SearchProductsAsync(searchTerm, categoryId);
                ViewBag.SearchTerm = searchTerm;
            }
            else if (categoryId.HasValue)
            {
                products = await _productService.GetProductsByCategoryAsync(categoryId.Value);
                var category = await _categoryService.GetCategoryByIdAsync(categoryId.Value);
                ViewBag.CategoryName = category?.Name;
                ViewBag.CategoryId = categoryId;
            }
            else
            {
                products = await _productService.GetAllProductsAsync();
            }

            // Phân trang
            var totalItems = products.Count();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            products = products.Skip((page - 1) * pageSize).Take(pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Categories = await _categoryService.GetAllCategoriesAsync();

            return View(products);
        }

        // GET: /Product/Details/5 hoặc /Product/Details/product-slug
        public async Task<IActionResult> Details(string id)
        {
            Product? product;

            // Kiểm tra xem id có phải là số hay không
            if (int.TryParse(id, out int productId))
            {
                product = await _productService.GetProductByIdAsync(productId);
            }
            else
            {
                // Nếu không phải số, xem như là slug
                product = await _productService.GetProductBySlugAsync(id);
            }

            if (product == null)
            {
                return NotFound();
            }

            // Ghi lại lượt xem sản phẩm
            string? userId = User.Identity?.IsAuthenticated == true ? User.FindFirst("sub")?.Value : null;
            await _productService.RecordProductViewAsync(
                product.Id,
                userId,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Request.Headers["User-Agent"].ToString());

            // Lấy sản phẩm liên quan
            ViewBag.RelatedProducts = await _productService.GetRelatedProductsAsync(product.Id, 4);

            // Lấy đánh giá sản phẩm
            ViewBag.Reviews = await _productService.GetProductReviewsAsync(product.Id);
            ViewBag.AverageRating = await _productService.GetAverageRatingAsync(product.Id);

            // Nếu người dùng đã đăng nhập, lấy các sản phẩm được đề xuất cá nhân hóa
            if (!string.IsNullOrEmpty(userId))
            {
                ViewBag.RecommendedProducts = await _recommendationService.GetPersonalizedRecommendationsAsync(userId, 4);
                ViewBag.RecentlyViewed = await _recommendationService.GetRecentlyViewedProductsAsync(userId, 4);
            }
            else
            {
                // Nếu chưa đăng nhập, lấy các sản phẩm phổ biến
                ViewBag.RecommendedProducts = await _recommendationService.GetPopularProductsAsync(4);
            }

            return View(product);
        }

        // POST: /Product/AddReview
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int productId, int rating, string comment)
        {
            if (rating < 1 || rating > 5)
            {
                ModelState.AddModelError("Rating", "Đánh giá phải từ 1 đến 5 sao");
            }

            if (string.IsNullOrWhiteSpace(comment))
            {
                ModelState.AddModelError("Comment", "Vui lòng nhập nội dung đánh giá");
            }

            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(Details), new { id = productId });
            }

            var userId = User.FindFirst("sub")?.Value;
            var userName = User.Identity?.Name;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userName))
            {
                return Unauthorized();
            }

            var review = new ProductReview
            {
                ProductId = productId,
                UserId = userId,
                UserName = userName,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.Now
            };

            await _productService.AddProductReviewAsync(review);

            return RedirectToAction(nameof(Details), new { id = productId });
        }

        // POST: /Product/AddToCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            if (quantity < 1)
            {
                return BadRequest("Số lượng phải lớn hơn 0");
            }

            string? userId = User.Identity?.IsAuthenticated == true 
                ? User.FindFirst("sub")?.Value 
                : HttpContext.Session.Id;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            await _cartService.AddToCartAsync(userId, productId, quantity);

            return RedirectToAction("Index", "Cart");
        }
    }
}