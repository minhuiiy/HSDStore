using CPTStore.Models;
using System.Threading.Tasks;

namespace CPTStore.Services.Interfaces
{
    public interface ISettingsService
    {
        Task<OrderConfirmationSettings> GetOrderConfirmationSettingsAsync();
        Task SaveOrderConfirmationSettingsAsync(OrderConfirmationSettings settings);
    }
}