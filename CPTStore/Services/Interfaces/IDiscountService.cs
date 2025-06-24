using CPTStore.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CPTStore.Services.Interfaces
{
    public interface IDiscountService
    {
        Task<IEnumerable<Discount>> GetAllDiscountsAsync();
        Task<Discount?> GetDiscountByIdAsync(int id);
        Task<Discount?> GetDiscountByCodeAsync(string code);
        Task<int> CreateDiscountAsync(Discount discount);
        Task UpdateDiscountAsync(Discount discount);
        Task DeleteDiscountAsync(int id);
        Task<bool> IsDiscountValidAsync(string code, decimal orderAmount);
        Task<decimal> CalculateDiscountAmountAsync(string code, decimal orderAmount);
        Task IncrementUsageCountAsync(int id);
        Task<IEnumerable<Discount>> GetActiveDiscountsAsync();
        
        // Phương thức bổ sung cho quản lý mã giảm giá
        Task<IEnumerable<Discount>> GetDiscountsAsync(string? searchTerm, bool? isActive, int page, int pageSize);
        Task<int> GetTotalDiscountCountAsync(string? searchTerm, bool? isActive);
    }
}