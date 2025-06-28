using Microsoft.AspNetCore.Mvc;
using CPTStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CPTStore.Controllers
{
    public class InventoryController : Controller
    {
        private readonly IInventoryService _inventoryService;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(IInventoryService inventoryService, ILogger<InventoryController> logger)
        {
            _inventoryService = inventoryService;
            _logger = logger;
        }

        // GET: /Inventory/SynchronizeStock
        [AllowAnonymous]
        public async Task<IActionResult> SynchronizeStock()
        {
            try
            {
                int syncCount = await _inventoryService.SynchronizeProductStockAsync();
                if (syncCount >= 0)
                {
                    TempData["SuccessMessage"] = $"Đã đồng bộ hóa thành công {syncCount} mục tồn kho.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Đã xảy ra lỗi khi đồng bộ hóa tồn kho.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đồng bộ hóa tồn kho");
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction("Index", "Home");
        }

        // GET: /Inventory/Index
        public IActionResult Index()
        {
            return View();
        }
        
        // GET: /Inventory/FixStockSynchronization
        [AllowAnonymous]
        public async Task<IActionResult> FixStockSynchronization()
        {
            try
            {
                int fixCount = await _inventoryService.FixInventoryStockSynchronizationAsync();
                if (fixCount >= 0)
                {
                    TempData["SuccessMessage"] = $"Đã sửa lỗi đồng bộ hóa thành công {fixCount} mục tồn kho.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Đã xảy ra lỗi khi sửa lỗi đồng bộ hóa tồn kho.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi sửa lỗi đồng bộ hóa tồn kho");
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction("Index", "Home");
        }
    }
}