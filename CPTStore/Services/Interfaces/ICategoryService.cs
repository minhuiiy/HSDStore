using CPTStore.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CPTStore.Services
{
    public interface ICategoryService
    {
        Task<IEnumerable<Category>> GetAllCategoriesAsync();
        Task<IEnumerable<Category>> GetMainCategoriesAsync();
        Task<IEnumerable<Category>> GetSubCategoriesAsync(int parentCategoryId);
        Task<Category?> GetCategoryByIdAsync(int id);
        Task<Category?> GetCategoryBySlugAsync(string slug);
        Task<int> CreateCategoryAsync(Category category);
        Task UpdateCategoryAsync(Category category);
        Task DeleteCategoryAsync(int id);
        Task<IEnumerable<Category>> GetCategoryPathAsync(int categoryId);
        Task<int> GetProductCountByCategoryAsync(int categoryId, bool includeSubcategories = true);
        
        // Các phương thức bổ sung theo yêu cầu của CategoriesController
        Task<IEnumerable<Category>> GetCategoriesAsync(string? searchTerm, int page, int pageSize);
        Task<int> GetTotalCategoryCountAsync(string? searchTerm);
        Task<int> AddCategoryAsync(Category category);
    }
}