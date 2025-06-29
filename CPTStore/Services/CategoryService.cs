using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CPTStore.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ApplicationDbContext _context;

        public CategoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            return await _context.Categories
                .Include(c => c.ParentCategory)
                .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetMainCategoriesAsync()
        {
            return await _context.Categories
                .Where(c => c.ParentCategoryId == null)
                .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetSubCategoriesAsync(int parentCategoryId)
        {
            return await _context.Categories
                .Where(c => c.ParentCategoryId == parentCategoryId)
                .ToListAsync();
        }

        public async Task<Category?> GetCategoryByIdAsync(int id)
        {
            return await _context.Categories
                .Include(c => c.ParentCategory)
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Category?> GetCategoryBySlugAsync(string slug)
        {
            return await _context.Categories
                .Include(c => c.ParentCategory)
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(c => c.Slug == slug);
        }

        public async Task<int> CreateCategoryAsync(Category category)
        {
            // Tạo slug từ tên danh mục nếu chưa có
            if (string.IsNullOrEmpty(category.Slug))
            {
                category.Slug = GenerateSlug(category.Name);
            }

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return category.Id;
        }

        public async Task UpdateCategoryAsync(Category category)
        {
            // Tạo slug từ tên danh mục nếu chưa có
            if (string.IsNullOrEmpty(category.Slug))
            {
                category.Slug = GenerateSlug(category.Name);
            }

            // Kiểm tra xem entity đã được theo dõi chưa
            var existingCategory = await _context.Categories.FindAsync(category.Id);
            if (existingCategory != null)
            {
                // Cập nhật các thuộc tính của entity đã được theo dõi
                _context.Entry(existingCategory).CurrentValues.SetValues(category);
            }
            else
            {
                // Nếu entity chưa được theo dõi, thêm vào context
                _context.Categories.Update(category);
            }
            
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCategoryAsync(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                // Kiểm tra xem danh mục có sản phẩm không
                var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
                if (hasProducts)
                {
                    throw new InvalidOperationException("Không thể xóa danh mục có sản phẩm");
                }

                // Kiểm tra xem danh mục có danh mục con không
                var hasSubCategories = await _context.Categories.AnyAsync(c => c.ParentCategoryId == id);
                if (hasSubCategories)
                {
                    throw new InvalidOperationException("Không thể xóa danh mục có danh mục con");
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Category>> GetCategoryPathAsync(int categoryId)
        {
            var result = new List<Category>();
            var category = await _context.Categories.FindAsync(categoryId);

            while (category != null)
            {
                result.Insert(0, category); // Thêm vào đầu danh sách

                if (category.ParentCategoryId.HasValue)
                {
                    category = await _context.Categories.FindAsync(category.ParentCategoryId.Value);
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        public async Task<int> GetProductCountByCategoryAsync(int categoryId, bool includeSubcategories = true)
        {
            if (includeSubcategories)
            {
                // Lấy danh sách tất cả các danh mục con (bao gồm cả danh mục hiện tại)
                var categoryIds = new List<int> { categoryId };
                var subCategories = await _context.Categories
                    .Where(c => c.ParentCategoryId == categoryId)
                    .ToListAsync();

                foreach (var subCategory in subCategories)
                {
                    categoryIds.Add(subCategory.Id);
                    // Nếu cần đệ quy sâu hơn, có thể gọi đệ quy ở đây
                }

                return await _context.Products
                    .Where(p => categoryIds.Contains(p.CategoryId))
                    .CountAsync();
            }
            else
            {
                return await _context.Products
                    .Where(p => p.CategoryId == categoryId)
                    .CountAsync();
            }
        }

        private string GenerateSlug(string title)
        {
            // Chuyển đổi tiếng Việt sang không dấu
            string slug = RemoveDiacritics(title);
            
            // Chuyển thành chữ thường
            slug = slug.ToLower();
            
            // Thay thế các ký tự không phải chữ cái hoặc số bằng dấu gạch ngang
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            
            // Thay thế khoảng trắng bằng dấu gạch ngang
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            
            // Thay thế nhiều dấu gạch ngang liên tiếp bằng một dấu gạch ngang
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\-+", "-");
            
            // Cắt bỏ dấu gạch ngang ở đầu và cuối
            slug = slug.Trim('-');
            
            return slug;
        }

        private string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
        
        // Các phương thức bổ sung theo yêu cầu của CategoriesController
        public async Task<IEnumerable<Category>> GetCategoriesAsync(string? searchTerm, int page, int pageSize)
        {
            var query = _context.Categories.AsQueryable();

            // Áp dụng tìm kiếm nếu có
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(c => c.Name.Contains(searchTerm) || 
                                      (c.Description != null && c.Description.Contains(searchTerm)));
            }

            // Phân trang
            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(c => c.ParentCategory)
                .Include(c => c.Products)
                .ToListAsync();
        }

        public async Task<int> GetTotalCategoryCountAsync(string? searchTerm)
        {
            var query = _context.Categories.AsQueryable();

            // Áp dụng tìm kiếm nếu có
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(c => c.Name.Contains(searchTerm) || 
                                      (c.Description != null && c.Description.Contains(searchTerm)));
            }

            return await query.CountAsync();
        }

        public async Task<int> AddCategoryAsync(Category category)
        {
            // Tạo slug từ tên danh mục nếu chưa có
            if (string.IsNullOrEmpty(category.Slug))
            {
                category.Slug = GenerateSlug(category.Name);
            }

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return category.Id;
        }
    }
}