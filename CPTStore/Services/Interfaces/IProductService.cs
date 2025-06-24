using CPTStore.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using CPTStore.Areas.Admin.ViewModels;

namespace CPTStore.Services.Interfaces
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllProductsAsync(bool includeOutOfStock = false);
        Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId, bool includeOutOfStock = false);
        Task<IEnumerable<Product>> GetProductsAsync(string? searchTerm, int? categoryId, int page, int pageSize);
        Task<Product?> GetProductByIdAsync(int id);
        Task<Product?> GetProductBySlugAsync(string slug);
        Task<int> CreateProductAsync(Product product);
        Task AddProductAsync(Product product);
        Task UpdateProductAsync(Product product);
        Task DeleteProductAsync(int id);
        Task<IEnumerable<Product>> SearchProductsAsync(string searchTerm, int? categoryId = null);
        Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int count = 4);
        Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count = 8);
        Task<IEnumerable<Product>> GetNewArrivalsAsync(int count = 8);
        Task<IEnumerable<Product>> GetBestSellersAsync(int count = 8);
        Task<IEnumerable<ProductReview>> GetProductReviewsAsync(int productId);
        Task AddProductReviewAsync(ProductReview review);
        Task<double> GetAverageRatingAsync(int productId);
        Task RecordProductViewAsync(int productId, string? userId, string ipAddress, string? userAgent);
        
        // Các phương thức bổ sung cho Dashboard
        Task<int> GetTotalProductCountAsync();
        Task<int> GetTotalProductCountAsync(string? searchTerm, int? categoryId);
        Task<IEnumerable<Product>> GetTopSellingProductsAsync(int count);
        
        // Phương thức bổ sung cho báo cáo
        Task<IEnumerable<TopSellingProductData>> GetTopSellingProductsAsync(DateTime startDate, DateTime endDate, int count);
    }
}