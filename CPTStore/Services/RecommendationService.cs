using CPTStore.Data;
using CPTStore.Models;
using Microsoft.EntityFrameworkCore;

namespace CPTStore.Services
{
    public class RecommendationService(ApplicationDbContext context) : IRecommendationService
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<IEnumerable<Product>> GetSimilarProductsAsync(int productId, int count = 5)
        {
            // Lấy thông tin sản phẩm hiện tại
            var currentProduct = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (currentProduct == null)
            {
                return [];
            }

            // Lấy sản phẩm tương tự dựa trên danh mục và giá
            var similarProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.CategoryId == currentProduct.CategoryId && 
                       p.Id != productId && 
                       p.IsAvailable &&
                       p.Price >= currentProduct.Price * 0.8m && 
                       p.Price <= currentProduct.Price * 1.2m)
                .OrderByDescending(p => p.ViewCount)
                .Take(count)
                .ToListAsync();

            // Nếu không đủ số lượng, bổ sung thêm sản phẩm từ cùng danh mục
            if (similarProducts.Count < count)
            {
                var additionalProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.CategoryId == currentProduct.CategoryId && 
                           p.Id != productId && 
                           p.IsAvailable &&
                           !similarProducts.Select(sp => sp.Id).Contains(p.Id))
                    .OrderByDescending(p => p.ViewCount)
                    .Take(count - similarProducts.Count)
                    .ToListAsync();

                similarProducts.AddRange(additionalProducts);
            }

            return similarProducts;
        }

        public async Task TrainRecommendationModelAsync()
        {
            // Trong một ứng dụng thực tế, đây là nơi bạn sẽ huấn luyện mô hình đề xuất
            // Ví dụ: sử dụng ML.NET, đào tạo mô hình dựa trên dữ liệu người dùng, v.v.
            
            // Đối với mục đích demo, chúng ta chỉ cần một phương thức giả
            await Task.Delay(100); // Giả lập thời gian huấn luyện
            
            // Có thể ghi log về việc huấn luyện mô hình
            Console.WriteLine($"Recommendation model trained at {DateTime.UtcNow}");
            
            await Task.CompletedTask;
        }

        public async Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int count = 4)
        {
            // Lấy thông tin sản phẩm hiện tại
            var currentProduct = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (currentProduct == null)
            {
                return [];
            }

            // Lấy sản phẩm cùng danh mục, không bao gồm sản phẩm hiện tại
            var relatedProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.CategoryId == currentProduct.CategoryId && p.Id != productId && p.IsAvailable)
                .OrderByDescending(p => p.ViewCount)
                .Take(count)
                .ToListAsync();

            // Nếu không đủ số lượng, bổ sung thêm sản phẩm từ danh mục cha (nếu có)
            if (relatedProducts.Count < count && currentProduct.Category?.ParentCategoryId != null)
            {
                var additionalProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.Category != null && p.Category.ParentCategoryId == currentProduct.Category.ParentCategoryId && 
                           p.Id != productId && 
                           !relatedProducts.Select(rp => rp.Id).Contains(p.Id) &&
                           p.IsAvailable)
                    .OrderByDescending(p => p.ViewCount)
                    .Take(count - relatedProducts.Count)
                    .ToListAsync();

                relatedProducts.AddRange(additionalProducts);
            }

            // Nếu vẫn không đủ, bổ sung thêm sản phẩm phổ biến
            if (relatedProducts.Count < count)
            {
                var popularProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.Id != productId && 
                           !relatedProducts.Select(rp => rp.Id).Contains(p.Id) &&
                           p.IsAvailable)
                    .OrderByDescending(p => p.ViewCount)
                    .Take(count - relatedProducts.Count)
                    .ToListAsync();

                relatedProducts.AddRange(popularProducts);
            }

            return relatedProducts;
        }

        public async Task<IEnumerable<Product>> GetPersonalizedRecommendationsAsync(string userId, int count = 8)
        {
            if (string.IsNullOrEmpty(userId))
            {
                // Nếu không có userId, trả về sản phẩm phổ biến
                return await GetPopularProductsAsync(count);
            }

            // Lấy danh sách sản phẩm đã xem gần đây
            var recentlyViewedProductIds = await _context.ProductViews
                .Where(pv => pv.UserId == userId)
                .OrderByDescending(pv => pv.ViewedAt)
                .Select(pv => pv.ProductId)
                .Take(10)
                .ToListAsync();

            // Lấy danh sách sản phẩm đã mua
            var purchasedProductIds = await _context.OrderItems
                .Where(oi => oi.Order != null && oi.Order.UserId == userId)
                .Select(oi => oi.ProductId)
                .Distinct()
                .ToListAsync();

            // Lấy danh mục của sản phẩm đã xem và đã mua
            var viewedCategories = await _context.Products
                .Where(p => recentlyViewedProductIds.Contains(p.Id))
                .Select(p => p.CategoryId)
                .Distinct()
                .ToListAsync();

            var purchasedCategories = await _context.Products
                .Where(p => purchasedProductIds.Contains(p.Id))
                .Select(p => p.CategoryId)
                .Distinct()
                .ToListAsync();

            // Kết hợp danh mục
            var interestedCategories = viewedCategories.Union(purchasedCategories).ToList();

            // Lấy sản phẩm từ danh mục quan tâm, không bao gồm sản phẩm đã mua
            var recommendedProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => interestedCategories.Contains(p.CategoryId) && 
                       !purchasedProductIds.Contains(p.Id) &&
                       p.IsAvailable)
                .OrderByDescending(p => p.ViewCount)
                .Take(count)
                .ToListAsync();

            // Nếu không đủ số lượng, bổ sung thêm sản phẩm phổ biến
            if (recommendedProducts.Count < count)
            {
                var popularProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => !recommendedProducts.Select(rp => rp.Id).Contains(p.Id) && 
                           !purchasedProductIds.Contains(p.Id) &&
                           p.IsAvailable)
                    .OrderByDescending(p => p.ViewCount)
                    .Take(count - recommendedProducts.Count)
                    .ToListAsync();

                recommendedProducts.AddRange(popularProducts);
            }

            return recommendedProducts;
        }

        public async Task<IEnumerable<Product>> GetPopularProductsAsync(int count = 8)
        {
            // Lấy sản phẩm phổ biến dựa trên đánh giá và lượt xem
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .OrderByDescending(p => p.ViewCount)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetFrequentlyBoughtTogetherAsync(int productId, int count = 4)
        {
            // Lấy danh sách đơn hàng có chứa sản phẩm hiện tại
            var orderIds = await _context.OrderItems
                .Where(oi => oi.ProductId == productId)
                .Select(oi => oi.OrderId)
                .ToListAsync();

            if (orderIds.Count == 0)
            {
                return [];
            }

            // Lấy sản phẩm thường được mua cùng (xuất hiện trong cùng đơn hàng)
            var frequentlyBoughtProductIds = await _context.OrderItems
                .Where(oi => orderIds.Contains(oi.OrderId) && oi.ProductId != productId)
                .GroupBy(oi => oi.ProductId)
                .Select(g => new { ProductId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Select(x => x.ProductId)
                .Take(count)
                .ToListAsync();

            if (frequentlyBoughtProductIds.Count == 0)
            {
                // Nếu không có sản phẩm thường được mua cùng, trả về sản phẩm liên quan
                return await GetRelatedProductsAsync(productId, count);
            }

            // Lấy thông tin chi tiết của sản phẩm
            var frequentlyBoughtProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => frequentlyBoughtProductIds.Contains(p.Id) && p.IsAvailable)
                .ToListAsync();

            // Sắp xếp theo thứ tự của frequentlyBoughtProductIds
            return frequentlyBoughtProductIds
                .Select(id => frequentlyBoughtProducts.FirstOrDefault(p => p.Id == id))
                .Where(p => p != null)
                .Select(p => p!)
                .ToList();
        }

        public async Task<IEnumerable<Product>> GetRecentlyViewedProductsAsync(string userId, int count = 8)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return [];
            }

            // Lấy danh sách sản phẩm đã xem gần đây
            var recentlyViewedProductIds = await _context.ProductViews
                .Where(pv => pv.UserId == userId)
                .OrderByDescending(pv => pv.ViewedAt)
                .Select(pv => pv.ProductId)
                .Distinct()
                .Take(count)
                .ToListAsync();

            if (recentlyViewedProductIds.Count == 0)
            {
                return [];
            }

            // Lấy thông tin chi tiết của sản phẩm
            var recentlyViewedProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => recentlyViewedProductIds.Contains(p.Id) && p.IsAvailable)
                .ToListAsync();

            // Sắp xếp theo thứ tự của recentlyViewedProductIds
            return recentlyViewedProductIds
                .Select(id => recentlyViewedProducts.FirstOrDefault(p => p.Id == id))
                .Where(p => p != null)
                .Select(p => p!)
                .ToList();
        }

        public async Task RecordProductViewAsync(string userId, int productId)
        {
            // Kiểm tra sản phẩm có tồn tại không
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return;
            }

            // Tăng lượt xem cho sản phẩm
            product.ViewCount += 1;
            _context.Products.Update(product);

            // Nếu có userId, lưu lịch sử xem sản phẩm
            if (!string.IsNullOrEmpty(userId))
            {
                var productView = new ProductView
                {
                    UserId = userId,
                    ProductId = productId,
                    ViewedAt = DateTime.UtcNow,
                    IPAddress = "0.0.0.0", // Giá trị mặc định, trong thực tế nên lấy từ HttpContext
                    UserAgent = "Unknown" // Giá trị mặc định, trong thực tế nên lấy từ HttpContext
                };

                _context.ProductViews.Add(productView);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Product>> GetTrendingProductsAsync(int count = 8)
        {
            // Lấy sản phẩm xu hướng dựa trên lượt xem gần đây
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            // Lấy sản phẩm có nhiều lượt xem trong 30 ngày qua
            var trendingProductIds = await _context.ProductViews
                .Where(pv => pv.ViewedAt >= thirtyDaysAgo)
                .GroupBy(pv => pv.ProductId)
                .Select(g => new { ProductId = g.Key, ViewCount = g.Count() })
                .OrderByDescending(x => x.ViewCount)
                .Select(x => x.ProductId)
                .Take(count)
                .ToListAsync();

            if (trendingProductIds.Count == 0)
            {
                // Nếu không có dữ liệu xu hướng, trả về sản phẩm phổ biến
                return await GetPopularProductsAsync(count);
            }

            // Lấy thông tin chi tiết của sản phẩm
            var trendingProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => trendingProductIds.Contains(p.Id) && p.IsAvailable)
                .ToListAsync();

            // Sắp xếp theo thứ tự của trendingProductIds
            return trendingProductIds
                .Select(id => trendingProducts.FirstOrDefault(p => p.Id == id))
                .Where(p => p != null)
                .Select(p => p!)
                .ToList();
        }

        public async Task RecordProductPurchaseAsync(string userId, int productId)
        {
            // Kiểm tra sản phẩm có tồn tại không
            var product = await _context.Products.FindAsync(productId);
            if (product == null || string.IsNullOrEmpty(userId))
            {
                return;
            }

            // Trong thực tế, có thể lưu thông tin mua hàng vào bảng riêng để phân tích
            // Ở đây chỉ ghi nhận việc mua hàng, thông tin chi tiết đã được lưu trong Order và OrderItem
            
            // Có thể thực hiện các hành động khác như cập nhật số lượng mua, tính toán xu hướng, v.v.
            
            // Lưu ý: Trong ứng dụng thực tế, việc này thường được xử lý khi tạo đơn hàng
            // nên phương thức này có thể chỉ cần ghi log hoặc cập nhật thông tin thống kê
            
            await Task.CompletedTask; // Placeholder, không thực hiện thao tác nào với cơ sở dữ liệu
        }
    }
}