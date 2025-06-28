using CPTStore.Models;
using System.Collections.Generic;

namespace CPTStore.ViewModels
{
    public class SearchViewModel
    {
        // Từ khóa tìm kiếm
        public string? SearchTerm { get; set; }
        
        // Alias cho SearchTerm để tương thích với tệp Search.cshtml
        public string? Query { get => SearchTerm; set => SearchTerm = value; }
        
        // Danh sách sản phẩm kết quả
        public List<Product>? Products { get; set; }
        
        // Danh sách danh mục để hiển thị bộ lọc
        public List<Category>? Categories { get; set; }
        
        // Thông tin phân trang
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 12;
        public int TotalItems { get; set; } = 0;
        
        // Thông tin lọc
        public int? CategoryId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string SortBy { get; set; } = "relevance";
        public bool InStock { get; set; } = false;
        
        // Tìm kiếm liên quan
        public List<string>? RelatedSearches { get; set; }
    }
}