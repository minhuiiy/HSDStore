using CPTStore.Models;

namespace CPTStore.Services
{
    public interface IInventoryService
    {
        Task<Inventory?> GetInventoryByProductIdAsync(int productId);
        Task<IEnumerable<Inventory>> GetAllInventoriesAsync();
        Task<IEnumerable<Inventory>> GetLowStockInventoriesAsync();
        Task<bool> IsInStockAsync(int productId, int quantity = 1);
        Task<bool> UpdateStockAsync(int productId, int quantity);
        Task<bool> DeductStockAsync(int productId, int quantity);
        Task<bool> RestockAsync(int productId, int quantity);
        Task<bool> CreateInventoryAsync(Inventory inventory);
        Task UpdateInventoryAsync(Inventory inventory);
        Task SendLowStockNotificationsAsync();
    }
}