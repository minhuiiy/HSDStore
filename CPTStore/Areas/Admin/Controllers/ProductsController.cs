using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using CPTStore.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using CPTStore.Data;
using Microsoft.EntityFrameworkCore;

namespace CPTStore.Areas.Admin.Controllers
{
    public partial class ProductsController : AdminControllerBase
    {
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _context;
        
        public ProductsController(IProductService productService, ICategoryService categoryService, IWebHostEnvironment webHostEnvironment, ApplicationDbContext context)
        {
            _productService = productService;
            _categoryService = categoryService;
            _webHostEnvironment = webHostEnvironment;
            _context = context;
        }

        // GET: Admin/Products
        public async Task<IActionResult> Index(string searchTerm, int? categoryId, int page = 1, int pageSize = 10)
        {
            var products = await _productService.GetProductsAsync(searchTerm, categoryId, page, pageSize);
            var categories = await _categoryService.GetAllCategoriesAsync();

            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            ViewBag.SearchTerm = searchTerm;
            ViewBag.CategoryId = categoryId;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(await _productService.GetTotalProductCountAsync(searchTerm, categoryId) / (double)pageSize);

            return View(products);
        }

        // GET: Admin/Products/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET: Admin/Products/Create
        public async Task<IActionResult> Create()
        {
            var categories = await _categoryService.GetAllCategoriesAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View();
        }

        // POST: Admin/Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile imageFile)
        {
            if (ModelState.IsValid)
            {
                // Xử lý tải lên hình ảnh
                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }

                    product.ImageUrl = "/images/products/" + uniqueFileName;
                }

                // Tạo slug từ tên sản phẩm
                product.Slug = CreateSlug(product.Name);
                product.CreatedAt = DateTime.Now;

                await _productService.AddProductAsync(product);
                return RedirectToAction(nameof(Index));
            }

            var categories = await _categoryService.GetAllCategoriesAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // GET: Admin/Products/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var categories = await _categoryService.GetAllCategoriesAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // POST: Admin/Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile imageFile)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Xử lý tải lên hình ảnh
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }

                        // Xóa ảnh cũ nếu có
                        var oldProduct = await _productService.GetProductByIdAsync(id);
                        if (!string.IsNullOrEmpty(oldProduct?.ImageUrl))
                        {
                            var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, oldProduct.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                        }

                        product.ImageUrl = "/images/products/" + uniqueFileName;
                    }

                    // Cập nhật slug nếu tên thay đổi
                    product.Slug = CreateSlug(product.Name);
                    product.UpdatedAt = DateTime.Now;

                    await _productService.UpdateProductAsync(product);
                }
                catch (Exception)
                {
                    if (!await ProductExists(product.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            var categories = await _categoryService.GetAllCategoriesAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // GET: Admin/Products/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: Admin/Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product != null)
            {
                // Xóa ảnh sản phẩm nếu có
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                await _productService.DeleteProductAsync(id);
            }

            return RedirectToAction(nameof(Index));
        }
        
        // GET: Admin/Products/DeleteAllProducts
        public IActionResult DeleteAllProducts()
        {
            return View();
        }
        
        // POST: Admin/Products/DeleteAllProducts
        [HttpPost, ActionName("DeleteAllProducts")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAllProductsConfirmed()
        {
            var products = await _context.Products.ToListAsync();
            
            foreach (var product in products)
            {
                // Xóa ảnh sản phẩm nếu có
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }
            }
            
            // Xóa tất cả sản phẩm
            _context.Products.RemoveRange(products);
            await _context.SaveChangesAsync();
            
            TempData["Message"] = $"Đã xóa {products.Count} sản phẩm mẫu thành công.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> ProductExists(int id)
        {
            return await _productService.GetProductByIdAsync(id) != null;
        }

        private static string CreateSlug(string title)
        {
            // Chuyển đổi tiếng Việt có dấu thành không dấu
            string slug = title.ToLower();
            slug = VietnameseARegex().Replace(slug, "a");
            slug = VietnameseERegex().Replace(slug, "e");
            slug = VietnameseIRegex().Replace(slug, "i");
            slug = VietnameseORegex().Replace(slug, "o");
            slug = VietnameseURegex().Replace(slug, "u");
            slug = VietnameseYRegex().Replace(slug, "y");
            slug = VietnameseDRegex().Replace(slug, "d");

            // Xóa ký tự đặc biệt
            slug = SpecialCharsRegex().Replace(slug, "");
            // Thay thế khoảng trắng bằng dấu gạch ngang
            slug = WhitespaceRegex().Replace(slug, "-");
            // Cắt bỏ dấu gạch ngang ở đầu và cuối
            slug = DashesRegex().Replace(slug, "");
            
            return slug;
        }
        
        [GeneratedRegex(@"[áàảãạâấầẩẫậăắằẳẵặ]")]
        private static partial Regex VietnameseARegex();
        
        [GeneratedRegex(@"[éèẻẽẹêếềểễệ]")]
        private static partial Regex VietnameseERegex();
        
        [GeneratedRegex(@"[íìỉĩị]")]
        private static partial Regex VietnameseIRegex();
        
        [GeneratedRegex(@"[óòỏõọôốồổỗộơớờởỡợ]")]
        private static partial Regex VietnameseORegex();
        
        [GeneratedRegex(@"[úùủũụưứừửữự]")]
        private static partial Regex VietnameseURegex();
        
        [GeneratedRegex(@"[ýỳỷỹỵ]")]
        private static partial Regex VietnameseYRegex();
        
        [GeneratedRegex(@"[đ]")]
        private static partial Regex VietnameseDRegex();
        
        [GeneratedRegex(@"[^a-z0-9\s-]")]
        private static partial Regex SpecialCharsRegex();
        
        [GeneratedRegex(@"[\s-]+")]
        private static partial Regex WhitespaceRegex();
        
        [GeneratedRegex(@"^-+|-+$")]
        private static partial Regex DashesRegex();

    }
}