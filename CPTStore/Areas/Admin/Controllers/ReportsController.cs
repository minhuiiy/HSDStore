using Microsoft.AspNetCore.Mvc;
using CPTStore.Services.Interfaces;
using CPTStore.Areas.Admin.ViewModels;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace CPTStore.Areas.Admin.Controllers
{
    public class ReportsController : AdminControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IProductService _productService;

        public ReportsController(IOrderService orderService, IProductService productService)
        {
            _orderService = orderService;
            _productService = productService;
        }

        // GET: Admin/Reports
        public IActionResult Index()
        {
            return View();
        }

        // GET: Admin/Reports/Sales
        public async Task<IActionResult> Sales(DateTime? startDate, DateTime? endDate)
        {
            // Mặc định là báo cáo tháng hiện tại
            if (!startDate.HasValue)
            {
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            }

            if (!endDate.HasValue)
            {
                endDate = DateTime.Now;
            }

            var orders = await _orderService.GetOrdersInPeriodAsync(startDate.Value, endDate.Value);
            
            var salesReport = new SalesReportViewModel
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                TotalOrders = orders.Count(),
                TotalRevenue = orders.Sum(o => o.TotalAmount),
                AverageOrderValue = orders.Any() ? orders.Average(o => o.TotalAmount) : 0,
                DailySales = GetDailySales(orders, startDate.Value, endDate.Value)
            };

            return View(salesReport);
        }

        // GET: Admin/Reports/Products
        public async Task<IActionResult> Products(DateTime? startDate, DateTime? endDate)
        {
            // Mặc định là báo cáo tháng hiện tại
            if (!startDate.HasValue)
            {
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            }

            if (!endDate.HasValue)
            {
                endDate = DateTime.Now;
            }

            var topProducts = await _productService.GetTopSellingProductsAsync(startDate.Value, endDate.Value, 10);
            
            var productReport = new ProductReportViewModel
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                TopSellingProducts = topProducts
            };

            return View(productReport);
        }

        // GET: Admin/Reports/Customers
        public async Task<IActionResult> Customers(DateTime? startDate, DateTime? endDate)
        {
            // Mặc định là báo cáo tháng hiện tại
            if (!startDate.HasValue)
            {
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            }

            if (!endDate.HasValue)
            {
                endDate = DateTime.Now;
            }

            var topCustomers = await _orderService.GetTopCustomersAsync(startDate.Value, endDate.Value, 10);
            
            var customerReport = new CustomerReportViewModel
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                TopCustomers = topCustomers
            };

            return View(customerReport);
        }

        private List<DailySalesData> GetDailySales(IEnumerable<Models.Order> orders, DateTime startDate, DateTime endDate)
        {
            var result = new List<DailySalesData>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dailyOrders = orders.Where(o => o.CreatedAt.Date == date.Date);
                result.Add(new DailySalesData
                {
                    Date = date,
                    OrderCount = dailyOrders.Count(),
                    Revenue = dailyOrders.Sum(o => o.TotalAmount)
                });
            }
            return result;
        }
    }

    // Các lớp dữ liệu báo cáo đã được chuyển sang ViewModels/ReportViewModels.cs
}