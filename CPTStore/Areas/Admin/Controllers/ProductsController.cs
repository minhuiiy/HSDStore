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
        public ProductsController(
            IProductService productService,
            ICategoryService categoryService,
            IWebHostEnvironment webHostEnvironment,
            ApplicationDbContext context,
            IInventoryService inventoryService)
        {
            _productService = productService;
            _categoryService = categoryService;
            _webHostEnvironment = webHostEnvironment;
            _context = context;
            _inventoryService = inventoryService;
        }

        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _context;
        private readonly IInventoryService _inventoryService;

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

        // GET: Admin/Products/SynchronizeInventory
        public async Task<IActionResult> SynchronizeInventory()
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

            // Nếu ModelState không hợp lệ, hiển thị lại form với danh sách categories
            ViewBag.Categories = new SelectList(await _categoryService.GetAllCategoriesAsync(), "Id", "Name", product.CategoryId);
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
                    // Lấy sản phẩm hiện tại từ database
                    var existingProduct = await _productService.GetProductByIdAsync(id);
                    if (existingProduct == null)
                    {
                        return NotFound();
                    }

                    // Lưu lại đường dẫn hình ảnh cũ
                    string oldImageUrl = existingProduct.ImageUrl ?? string.Empty;

                    // Cập nhật các thuộc tính của sản phẩm hiện tại
                    existingProduct.Name = product.Name;
                    existingProduct.SKU = product.SKU;
                    existingProduct.Description = product.Description;
                    existingProduct.ShortDescription = product.ShortDescription;
                    existingProduct.Price = product.Price;
                    // OriginalPrice là thuộc tính chỉ đọc, được tính từ Price + Discount
                    existingProduct.Discount = product.Discount;
                    existingProduct.CategoryId = product.CategoryId;
                    existingProduct.Stock = product.Stock;
                    existingProduct.IsAvailable = product.IsAvailable;
                    existingProduct.MetaDescription = product.MetaDescription;
                    existingProduct.MetaKeywords = product.MetaKeywords;
                    existingProduct.UpdatedAt = DateTime.Now;

                    // Xử lý tải lên hình ảnh mới
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
                        if (!string.IsNullOrEmpty(oldImageUrl))
                        {
                            var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, oldImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                        }

                        existingProduct.ImageUrl = "/images/products/" + uniqueFileName;
                    }
                    else if (product.ImageUrl != null)
                    {
                        // Giữ lại URL hình ảnh hiện tại nếu không có hình ảnh mới được tải lên
                        existingProduct.ImageUrl = product.ImageUrl;
                    }

                    // Cập nhật slug nếu tên thay đổi
                    existingProduct.Slug = CreateSlug(existingProduct.Name);

                    // Sử dụng phương thức UpdateProductAsync đã cập nhật
                    await _productService.UpdateProductAsync(existingProduct);
                }
                catch (Exception ex)
                {
                    // Ghi log lỗi
                    Console.WriteLine($"Lỗi khi cập nhật sản phẩm: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                    
                    if (!await ProductExists(product.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        // Thêm lỗi vào ModelState
                        ModelState.AddModelError("", $"Lỗi khi cập nhật sản phẩm: {ex.Message}");
                        ViewBag.Categories = new SelectList(await _categoryService.GetAllCategoriesAsync(), "Id", "Name", product.CategoryId);
                        return View(product);
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
            try
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
            catch (InvalidOperationException ex)
            {
                // Ghi log lỗi
                Console.WriteLine($"Lỗi khi xóa sản phẩm: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                
                // Thêm lỗi vào ModelState
                ModelState.AddModelError("", $"Không thể xóa sản phẩm: {ex.Message}");
                
                // Hiển thị lại trang xóa với thông báo lỗi
                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    return NotFound();
                }
                return View(product);
            }
            catch (Exception ex)
            {
                // Ghi log lỗi
                Console.WriteLine($"Lỗi khi xóa sản phẩm: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                
                // Thêm thông báo lỗi vào ModelState
                ModelState.AddModelError(string.Empty, $"Không thể xóa sản phẩm: {ex.Message}");
                
                // Lấy lại thông tin sản phẩm để hiển thị trang xóa với thông báo lỗi
                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    return NotFound();
                }
                
                return View(product);
            }
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