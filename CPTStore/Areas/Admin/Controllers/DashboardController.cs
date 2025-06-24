using Microsoft.AspNetCore.Mvc;
using CPTStore.Services.Interfaces;
using CPTStore.Areas.Admin.ViewModels;
using System.Threading.Tasks;
using CPTStore.Services;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using CPTStore.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace CPTStore.Areas.Admin.Controllers
{
    public class DashboardController : AdminControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            IOrderService orderService,
            IProductService productService,
            ICategoryService categoryService,
            UserManager<ApplicationUser> userManager,
            ILogger<DashboardController> logger)
        {
            _orderService = orderService;
            _productService = productService;
            _categoryService = categoryService;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new DashboardViewModel();

            try
            {
                // Lấy thông tin tổng quan
                viewModel.TotalOrders = await _orderService.GetTotalOrderCountAsync();
                viewModel.TotalProducts = await _productService.GetTotalProductCountAsync();
                viewModel.TotalCategories = await _categoryService.GetTotalCategoryCountAsync(null);
                viewModel.TotalUsers = _userManager.Users.Count();

                try
                {
                    // Lấy đơn hàng gần đây
                    viewModel.RecentOrders = await _orderService.GetRecentOrdersAsync(5);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi lấy đơn hàng gần đây: {Message}", ex.Message);
                    viewModel.RecentOrders = new List<Order>(); // Trả về danh sách trống nếu có lỗi
                }

                try
                {
                    // Lấy sản phẩm bán chạy
                    viewModel.TopSellingProducts = await _productService.GetTopSellingProductsAsync(5);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi lấy sản phẩm bán chạy: {Message}", ex.Message);
                    viewModel.TopSellingProducts = new List<Product>(); // Trả về danh sách trống nếu có lỗi
                }

                try
                {
                    // Lấy doanh thu theo tháng
                    viewModel.MonthlyRevenue = await _orderService.GetMonthlyRevenueAsync(6); // 6 tháng gần nhất
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi lấy doanh thu theo tháng: {Message}", ex.Message);
                    
                    // Tạo dữ liệu mặc định cho biểu đồ doanh thu
                    var monthlyRevenue = new Dictionary<string, decimal>();
                    var endDate = DateTime.Now;
                    
                    for (int i = 0; i < 6; i++)
                    {
                        var date = endDate.AddMonths(-i);
                        var monthYear = $"{date.Month}/{date.Year}";
                        monthlyRevenue[monthYear] = 0;
                    }
                    
                    viewModel.MonthlyRevenue = monthlyRevenue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải dữ liệu cho Dashboard: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải dữ liệu. Vui lòng thử lại sau.";
                
                // Đảm bảo viewModel có dữ liệu mặc định hợp lệ
                viewModel.TotalOrders = 0;
                viewModel.TotalProducts = 0;
                viewModel.TotalCategories = 0;
                viewModel.TotalUsers = 0;
                viewModel.RecentOrders = new List<Order>();
                viewModel.TopSellingProducts = new List<Product>();
                
                // Tạo dữ liệu mặc định cho biểu đồ doanh thu
                var monthlyRevenue = new Dictionary<string, decimal>();
                var endDate = DateTime.Now;
                
                for (int i = 0; i < 6; i++)
                {
                    var date = endDate.AddMonths(-i);
                    var monthYear = $"{date.Month}/{date.Year}";
                    monthlyRevenue[monthYear] = 0;
                }
                
                viewModel.MonthlyRevenue = monthlyRevenue;
            }

            return View(viewModel);
        }
    }
}