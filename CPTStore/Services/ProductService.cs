using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CPTStore.Areas.Admin.ViewModels;

namespace CPTStore.Services
{
    public partial class ProductService(ApplicationDbContext context) : IProductService
    {
        private readonly ApplicationDbContext _context = context;
        
        public async Task<IEnumerable<Product>> GetProductsAsync(string? searchTerm, int? categoryId, int page, int pageSize)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .AsQueryable();
                
            // Áp dụng bộ lọc tìm kiếm nếu có
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => p.Name.Contains(searchTerm) || 
                                    (p.Description != null && p.Description.Contains(searchTerm)) ||
                                    p.SKU.Contains(searchTerm));
            }
            
            // Lọc theo danh mục nếu có
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value || 
                                        (p.Category != null && p.Category.ParentCategoryId == categoryId.Value));
            }
            
            // Phân trang
            return await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        
        public async Task AddProductAsync(Product product)
        {
            // Tạo slug nếu chưa có
            if (string.IsNullOrEmpty(product.Slug))
            {
                product.Slug = GenerateSlug(product.Name);
            }
            
            _context.Products.Add(product);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"Lỗi cơ sở dữ liệu khi thêm sản phẩm: {product.Name}, Lỗi: {dbEx.Message}");
                if (dbEx.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
                throw new InvalidOperationException($"Không thể thêm sản phẩm do lỗi cơ sở dữ liệu: {dbEx.Message}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi thêm sản phẩm: {product.Name}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Không thể thêm sản phẩm: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<Product>> GetAllProductsAsync(bool includeOutOfStock = false)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .AsQueryable();

            if (!includeOutOfStock)
            {
                query = query.Where(p => p.Stock > 0);
            }

            return await query.ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId, bool includeOutOfStock = false)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.CategoryId == categoryId || (p.Category != null && p.Category.ParentCategoryId == categoryId))
                .AsQueryable();

            if (!includeOutOfStock)
            {
                query = query.Where(p => p.Stock > 0);
            }

            return await query.ToListAsync();
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Product?> GetProductBySlugAsync(string slug)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(p => p.Slug == slug);
        }

        public async Task<int> CreateProductAsync(Product product)
        {
            // Tạo slug từ tên sản phẩm nếu chưa có
            if (string.IsNullOrEmpty(product.Slug))
            {
                product.Slug = GenerateSlug(product.Name);
            }

            _context.Products.Add(product);
            try
            {
                await _context.SaveChangesAsync();
                return product.Id;
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"Lỗi cơ sở dữ liệu khi tạo sản phẩm: {product.Name}, Lỗi: {dbEx.Message}");
                if (dbEx.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
                throw new InvalidOperationException($"Không thể tạo sản phẩm do lỗi cơ sở dữ liệu: {dbEx.Message}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tạo sản phẩm: {product.Name}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Không thể tạo sản phẩm: {ex.Message}", ex);
            }
        }

        public async Task UpdateProductAsync(Product product)
        {
            // Tạo slug từ tên sản phẩm nếu chưa có
            if (string.IsNullOrEmpty(product.Slug))
            {
                product.Slug = GenerateSlug(product.Name);
            }

            _context.Products.Update(product);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"Lỗi cơ sở dữ liệu khi cập nhật sản phẩm: {product.Name}, Lỗi: {dbEx.Message}");
                if (dbEx.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
                throw new InvalidOperationException($"Không thể cập nhật sản phẩm do lỗi cơ sở dữ liệu: {dbEx.Message}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi cập nhật sản phẩm: {product.Name}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Không thể cập nhật sản phẩm: {ex.Message}", ex);
            }
        }

        public async Task DeleteProductAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException dbEx)
                {
                    Console.WriteLine($"Lỗi cơ sở dữ liệu khi xóa sản phẩm ID: {id}, Lỗi: {dbEx.Message}");
                    if (dbEx.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
                    throw new InvalidOperationException($"Không thể xóa sản phẩm do lỗi cơ sở dữ liệu: {dbEx.Message}", dbEx);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi khi xóa sản phẩm ID: {id}, Lỗi: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    throw new InvalidOperationException($"Không thể xóa sản phẩm: {ex.Message}", ex);
                }
            }
        }

        public async Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm, int? categoryId = null)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.Name.Contains(searchTerm) || 
                            (p.Description != null && p.Description.Contains(searchTerm)) ||
                            p.SKU.Contains(searchTerm))
                .AsQueryable();

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value || 
                                        (p.Category != null && p.Category.ParentCategoryId == categoryId.Value));
            }

            return await query.ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int count = 4)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return Array.Empty<Product>();
            }

            return await _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.Id != productId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count = 8)
        {
            return await _context.Products
                .Where(p => p.IsAvailable)
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetNewArrivalsAsync(int count = 8)
        {
            return await _context.Products
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetBestSellersAsync(int count = 8)
        {
            // Lấy sản phẩm bán chạy nhất dựa trên số lượng đơn hàng
            return await _context.OrderItems
                .GroupBy(oi => oi.ProductId)
                .Select(g => new { ProductId = g.Key, Count = g.Sum(oi => oi.Quantity) })
                .OrderByDescending(x => x.Count)
                .Take(count)
                .Join(_context.Products,
                    bestseller => bestseller.ProductId,
                    product => product.Id,
                    (bestseller, product) => product)
                .ToListAsync();
        }

        public async Task<IEnumerable<ProductReview>> GetProductReviewsAsync(int productId)
        {
            return await _context.ProductReviews
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task AddProductReviewAsync(ProductReview review)
        {
            _context.ProductReviews.Add(review);
            try
            {
                await _context.SaveChangesAsync();
                
                // Cập nhật điểm đánh giá trung bình của sản phẩm
                await UpdateProductRatingAsync();
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"Lỗi cơ sở dữ liệu khi thêm đánh giá sản phẩm ID: {review.ProductId}, Lỗi: {dbEx.Message}");
                if (dbEx.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
                throw new InvalidOperationException($"Không thể thêm đánh giá sản phẩm do lỗi cơ sở dữ liệu: {dbEx.Message}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi thêm đánh giá sản phẩm ID: {review.ProductId}, Lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Không thể thêm đánh giá sản phẩm: {ex.Message}", ex);
            }
        }

        private static async Task UpdateProductRatingAsync()
        {
            // Phương thức này không cần thực hiện gì vì chúng ta sẽ tính toán rating khi cần
            // thông qua phương thức GetAverageRatingAsync
            await Task.CompletedTask;
        }

        public async Task<double> GetAverageRatingAsync(int productId)
        {
            var reviews = await _context.ProductReviews
                .Where(r => r.ProductId == productId)
                .ToListAsync();

            if (reviews.Count > 0)
            {
                return reviews.Average(r => r.Rating);
            }

            return 0; // Trả về 0 nếu không có đánh giá nào
        }

        private static string GenerateSlug(string title)
        {
            // Chuyển đổi tiếng Việt sang không dấu
            string slug = RemoveDiacritics(title);
            
            // Chuyển thành chữ thường
            slug = slug.ToLower();
            
            // Thay thế các ký tự không phải chữ cái hoặc số bằng dấu gạch ngang
            slug = NonAlphanumericRegex().Replace(slug, "");
            
            // Thay thế khoảng trắng bằng dấu gạch ngang
            slug = WhitespaceRegex().Replace(slug, "-");
            
            // Thay thế nhiều dấu gạch ngang liên tiếp bằng một dấu gạch ngang
            slug = MultipleDashesRegex().Replace(slug, "-");
            
            // Cắt bỏ dấu gạch ngang ở đầu và cuối
            slug = slug.Trim('-');
            
            return slug;
        }
        
        [GeneratedRegex(@"[^a-z0-9\s-]")]
        private static partial Regex NonAlphanumericRegex();
        
        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();
        
        [GeneratedRegex(@"\-+")]
        private static partial Regex MultipleDashesRegex();

        private static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        public async Task RecordProductViewAsync(int productId, string? userId, string ipAddress, string? userAgent)
        {
            var productView = new ProductView
            {
                ProductId = productId,
                UserId = userId,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ViewedAt = DateTime.Now
            };

            _context.Add(productView);
            await _context.SaveChangesAsync();
        }

        // Các phương thức bổ sung cho Dashboard
        public async Task<int> GetTotalProductCountAsync()
        {
            return await _context.Products.CountAsync();
        }
        
        public async Task<int> GetTotalProductCountAsync(string? searchTerm, int? categoryId)
        {
            var query = _context.Products.AsQueryable();
            
            // Áp dụng bộ lọc tìm kiếm nếu có
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => p.Name.Contains(searchTerm) || 
                                    (p.Description != null && p.Description.Contains(searchTerm)) ||
                                    p.SKU.Contains(searchTerm));
            }
            
            // Lọc theo danh mục nếu có
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value || 
                                        (p.Category != null && p.Category.ParentCategoryId == categoryId.Value));
            }
            
            return await query.CountAsync();
        }

        public async Task<IEnumerable<Product>> GetTopSellingProductsAsync(int count)
        {
            // Lấy các sản phẩm bán chạy nhất dựa trên số lượng đã bán
            var topSellingProductIds = await _context.OrderItems
                .GroupBy(oi => oi.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    TotalQuantity = g.Sum(oi => oi.Quantity)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(count)
                .Select(x => x.ProductId)
                .ToListAsync();

            // Lấy thông tin đầy đủ của các sản phẩm
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => topSellingProductIds.Contains(p.Id))
                .ToListAsync();

            // Sắp xếp lại theo thứ tự của topSellingProductIds
            return products.OrderBy(p => topSellingProductIds.IndexOf(p.Id)).ToList();
        }
        
        public async Task<IEnumerable<TopSellingProductData>> GetTopSellingProductsAsync(DateTime startDate, DateTime endDate, int count)
        {
            // Lấy các sản phẩm bán chạy nhất trong khoảng thời gian
            var topSellingProducts = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Where(oi => oi.Order != null && oi.Order.CreatedAt >= startDate && oi.Order.CreatedAt <= endDate && oi.Product != null)
                .GroupBy(oi => new { oi.ProductId, ProductName = oi.Product != null ? oi.Product.Name : "Unknown" })
                .Select(g => new TopSellingProductData
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    QuantitySold = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.Price * oi.Quantity)
                })
                .OrderByDescending(x => x.QuantitySold)
                .Take(count)
                .ToListAsync();

            return topSellingProducts;
        }
    }
}