using Microsoft.AspNetCore.Mvc;
using CPTStore.Models;
using CPTStore.Services;
using CPTStore.Services.Interfaces;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CPTStore.Data;

namespace CPTStore.Areas.Admin.Controllers
{
    public class InventoryController : AdminControllerBase
    {
        private readonly IInventoryService _inventoryService;
        private readonly IProductService _productService;
        private readonly ILogger<InventoryController> _logger;
        private readonly ApplicationDbContext _context;

        public InventoryController(
            IInventoryService inventoryService,
            IProductService productService,
            ILogger<InventoryController> logger,
            ApplicationDbContext context)
        {
            _inventoryService = inventoryService;
            _productService = productService;
            _logger = logger;
            _context = context;
        }

        // GET: Admin/Inventory
        public async Task<IActionResult> Index(string searchTerm, int page = 1, int pageSize = 10)
        {
            var inventories = await _inventoryService.GetAllInventoriesAsync();
            var inventoryList = inventories.ToList();

            // Lọc theo từ khóa tìm kiếm nếu có
            if (!string.IsNullOrEmpty(searchTerm))
            {
                inventoryList = inventoryList
                    .Where(i => i.Product != null && 
                           (i.Product.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                            i.Product.SKU.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            // Phân trang
            var totalItems = inventoryList.Count;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            var paginatedInventories = inventoryList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.SearchTerm = searchTerm;

            return View(paginatedInventories);
        }

        // GET: Admin/Inventory/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var inventory = await _inventoryService.GetInventoryByIdAsync(id);
            if (inventory == null)
            {
                return NotFound();
            }

            return View(inventory);
        }

        // POST: Admin/Inventory/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Inventory inventory)
        {
            if (id != inventory.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await _inventoryService.UpdateInventoryAsync(inventory);
                    TempData["Success"] = "Cập nhật tồn kho thành công";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi cập nhật tồn kho");
                    ModelState.AddModelError("", $"Lỗi khi cập nhật: {ex.Message}");
                }
            }

            return View(inventory);
        }

        // GET: Admin/Inventory/Synchronize
        public IActionResult Synchronize()
        {
            return View();
        }
        
        // POST: Admin/Inventory/Synchronize
        [HttpPost, ActionName("Synchronize")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SynchronizeConfirmed()
        {
            try
            {
                int syncCount = await _inventoryService.SynchronizeProductStockAsync();
                TempData["Success"] = $"Đã đồng bộ hóa {syncCount} bản ghi tồn kho thành công";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi đồng bộ hóa tồn kho: {ex.Message}";
            }
            
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Inventory/Synchronize
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Synchronize(bool confirm)
        {
            if (confirm)
            {
                try
                {
                    int syncCount = await _inventoryService.SynchronizeProductStockAsync();
                    TempData["Success"] = $"Đã đồng bộ hóa {syncCount} bản ghi tồn kho";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi đồng bộ hóa tồn kho");
                    TempData["Error"] = $"Lỗi khi đồng bộ hóa tồn kho: {ex.Message}";
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Inventory/LowStock
        public async Task<IActionResult> LowStock()
        {
            var lowStockInventories = await _inventoryService.GetLowStockInventoriesAsync();
            return View(lowStockInventories);
        }
    }
}