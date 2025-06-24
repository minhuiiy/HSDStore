using Microsoft.AspNetCore.Mvc;
using CPTStore.Services.Interfaces;
using CPTStore.Models;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.AspNetCore.Http;
using CPTStore.Services;

namespace CPTStore.Areas.Admin.Controllers
{
    public class CategoriesController(ICategoryService categoryService, IWebHostEnvironment webHostEnvironment) : AdminControllerBase
    {
        private readonly ICategoryService _categoryService = categoryService;
        private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;

        // GET: Admin/Categories
        public async Task<IActionResult> Index(string searchTerm, int page = 1, int pageSize = 10)
        {
            var categories = await _categoryService.GetCategoriesAsync(searchTerm, page, pageSize);

            ViewBag.SearchTerm = searchTerm;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(await _categoryService.GetTotalCategoryCountAsync(searchTerm) / (double)pageSize);

            return View(categories);
        }

        // GET: Admin/Categories/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // GET: Admin/Categories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category, IFormFile imageFile)
        {
            if (ModelState.IsValid)
            {
                // Xử lý tải lên hình ảnh
                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "categories");
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

                    category.ImageUrl = "/images/categories/" + uniqueFileName;
                }

                // Tạo slug từ tên danh mục
                category.Slug = CreateSlug(category.Name);
                category.CreatedAt = DateTime.Now;

                await _categoryService.AddCategoryAsync(category);
                return RedirectToAction(nameof(Index));
            }

            return View(category);
        }

        // GET: Admin/Categories/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // POST: Admin/Categories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category, IFormFile imageFile)
        {
            if (id != category.Id)
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
                        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "categories");
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
                        var oldCategory = await _categoryService.GetCategoryByIdAsync(id);
                        if (!string.IsNullOrEmpty(oldCategory?.ImageUrl))
                        {
                            var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, oldCategory.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                        }

                        category.ImageUrl = "/images/categories/" + uniqueFileName;
                    }

                    // Cập nhật slug nếu tên thay đổi
                    category.Slug = CreateSlug(category.Name);
                    category.UpdatedAt = DateTime.Now;

                    await _categoryService.UpdateCategoryAsync(category);
                }
                catch (Exception)
                {
                    if (!await CategoryExists(category.Id))
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

            return View(category);
        }

        // GET: Admin/Categories/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // POST: Admin/Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            if (category != null)
            {
                // Xóa ảnh danh mục nếu có
                if (!string.IsNullOrEmpty(category.ImageUrl))
                {
                    var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, category.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                await _categoryService.DeleteCategoryAsync(id);
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> CategoryExists(int id)
        {
            return await _categoryService.GetCategoryByIdAsync(id) != null;
        }

        private static string CreateSlug(string title)
        {
            // Chuyển đổi tiếng Việt có dấu thành không dấu
            string slug = title.ToLower();
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[áàảãạâấầẩẫậăắằẳẵặ]", "a");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[éèẻẽẹêếềểễệ]", "e");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[íìỉĩị]", "i");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[óòỏõọôốồổỗộơớờởỡợ]", "o");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[úùủũụưứừửữự]", "u");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[ýỳỷỹỵ]", "y");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[đ]", "d");

            // Xóa ký tự đặc biệt
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            // Thay thế khoảng trắng bằng dấu gạch ngang
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[\s-]+", "-");
            // Cắt bỏ dấu gạch ngang ở đầu và cuối
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"^-+|-+$", "");

            return slug;
        }
    }
}