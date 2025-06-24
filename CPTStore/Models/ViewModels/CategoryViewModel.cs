using System.Collections.Generic;

namespace CPTStore.Models.ViewModels
{
    public class CategoryViewModel
    {
        // Thông tin danh mục
        public Category? Category { get; set; }
        
        // Danh sách sản phẩm
        public List<Product>? Products { get; set; }
        
        // Danh sách danh mục
        public List<Category>? Categories { get; set; }
        
        // Thông tin phân trang
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 12;
        public int TotalItems { get; set; } = 0;
        
        // Thông tin lọc và sắp xếp
        public string? Search { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string SortBy { get; set; } = "newest";
        public bool InStock { get; set; } = false;
    }
}