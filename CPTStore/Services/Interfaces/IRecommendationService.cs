using CPTStore.Models;

namespace CPTStore.Services
{
    public interface IRecommendationService
    {
        Task<IEnumerable<Product>> GetPersonalizedRecommendationsAsync(string userId, int count = 5);
        Task<IEnumerable<Product>> GetSimilarProductsAsync(int productId, int count = 5);
        Task<IEnumerable<Product>> GetFrequentlyBoughtTogetherAsync(int productId, int count = 5);
        Task<IEnumerable<Product>> GetPopularProductsAsync(int count = 5);
        Task<IEnumerable<Product>> GetRecentlyViewedProductsAsync(string userId, int count = 5);
        Task RecordProductViewAsync(string userId, int productId);
        Task RecordProductPurchaseAsync(string userId, int productId);
        Task TrainRecommendationModelAsync();
    }
}