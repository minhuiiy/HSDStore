using CPTStore.Data;
using CPTStore.Models;
using Microsoft.EntityFrameworkCore;

namespace CPTStore.Services
{
    public class InventoryService(ApplicationDbContext context, IEmailService emailService) : IInventoryService
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IEmailService _emailService = emailService;

        public async Task<Inventory?> GetInventoryByProductIdAsync(int productId)
        {
            return await _context.Inventories
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.ProductId == productId);
        }

        public async Task<IEnumerable<Inventory>> GetAllInventoriesAsync()
        {
            return await _context.Inventories
                .Include(i => i.Product)
                .ToListAsync();
        }

        public async Task<IEnumerable<Inventory>> GetLowStockInventoriesAsync()
        {
            return await _context.Inventories
                .Include(i => i.Product)
                .Where(i => i.Quantity <= i.MinimumStockLevel)
                .ToListAsync();
        }

        public async Task<bool> IsInStockAsync(int productId, int quantity = 1)
        {
            var inventory = await GetInventoryByProductIdAsync(productId);
            return inventory != null && inventory.Quantity >= quantity;
        }

        public async Task<bool> UpdateStockAsync(int productId, int quantity)
        {
            var inventory = await GetInventoryByProductIdAsync(productId);
            if (inventory == null)
            {
                return false;
            }

            inventory.Quantity = quantity;
            inventory.UpdatedAt = DateTime.UtcNow;

            _context.Inventories.Update(inventory);
            await _context.SaveChangesAsync();

            // Kiểm tra nếu tồn kho thấp thì gửi thông báo
            if (inventory.StockQuantity <= inventory.LowStockThreshold)
            {
                await _emailService.SendLowStockNotificationAsync(productId);
            }

            return true;
        }

        public async Task<bool> DeductStockAsync(int productId, int quantity)
        {
            var inventory = await GetInventoryByProductIdAsync(productId);
            if (inventory == null || inventory.Quantity < quantity)
            {
                return false;
            }

            inventory.Quantity -= quantity;
            inventory.UpdatedAt = DateTime.UtcNow;

            _context.Inventories.Update(inventory);
            await _context.SaveChangesAsync();

            // Kiểm tra nếu tồn kho thấp thì gửi thông báo
            if (inventory.StockQuantity <= inventory.LowStockThreshold)
            {
                await _emailService.SendLowStockNotificationAsync(productId);
            }

            return true;
        }

        public async Task<bool> RestockAsync(int productId, int quantity)
        {
            var inventory = await GetInventoryByProductIdAsync(productId);
            if (inventory == null)
            {
                return false;
            }

            inventory.Quantity += quantity;
            inventory.UpdatedAt = DateTime.UtcNow;

            _context.Inventories.Update(inventory);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> CreateInventoryAsync(Inventory inventory)
        {
            // Kiểm tra xem sản phẩm đã có tồn kho chưa
            var existingInventory = await GetInventoryByProductIdAsync(inventory.ProductId);
            if (existingInventory != null)
            {
                return false;
            }

            inventory.UpdatedAt = DateTime.UtcNow;
            _context.Inventories.Add(inventory);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task UpdateInventoryAsync(Inventory inventory)
        {
            inventory.UpdatedAt = DateTime.UtcNow;
            _context.Inventories.Update(inventory);
            await _context.SaveChangesAsync();
        }

        public async Task SendLowStockNotificationsAsync()
        {
            var lowStockItems = await GetLowStockInventoriesAsync();
            foreach (var item in lowStockItems)
            {
                await _emailService.SendLowStockNotificationAsync(item.ProductId);
            }
        }
    }
}