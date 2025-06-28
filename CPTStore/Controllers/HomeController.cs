using System.Diagnostics;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using CPTStore.Services;
using Microsoft.AspNetCore.Mvc;

namespace CPTStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;

        public HomeController(ILogger<HomeController> logger, IProductService productService, ICategoryService categoryService)
        {
            _logger = logger;
            _productService = productService;
            _categoryService = categoryService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Categories = await _categoryService.GetAllCategoriesAsync();
            ViewBag.FeaturedProducts = await _productService.GetFeaturedProductsAsync(8);
            ViewBag.NewProducts = await _productService.GetNewArrivalsAsync(8);
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { 
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                Message = "Đã xảy ra lỗi trong quá trình xử lý yêu cầu của bạn"
            });
        }
    }
}
