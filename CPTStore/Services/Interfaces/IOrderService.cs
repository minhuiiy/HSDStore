using System;
using CPTStore.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using CPTStore.Areas.Admin.ViewModels;
using CPTStore.ViewModels;

namespace CPTStore.Services.Interfaces
{
    public interface IOrderService
    {
        Task<Order> CreateOrderAsync(string userId, string customerName, string phoneNumber, string address, string? city, string? postalCode, PaymentMethod paymentMethod, string? notes);
        Task<Order?> GetOrderAsync(int id);
        Task<Order?> GetOrderByIdAsync(int id);
        Task<Order?> GetOrderByNumberAsync(string orderNumber);
        Task<IEnumerable<Order>> GetUserOrdersAsync(string userId);
        Task<IEnumerable<Order>> GetAllOrdersAsync(OrderStatus? status = null);
        Task<IEnumerable<Order>> GetOrdersAsync(string? searchTerm, string? status, DateTime? fromDate, DateTime? toDate, int page, int pageSize);
        Task UpdateOrderAsync(Order order);
        Task UpdateOrderStatusAsync(int id, OrderStatus status);
        Task UpdatePaymentStatusAsync(int id, PaymentStatus status, string? transactionId = null);
        Task<bool> CancelOrderAsync(int id);
        Task DeleteOrderAsync(int id);
        Task<byte[]> GenerateInvoicePdfAsync(int orderId);
        Task SendOrderConfirmationEmailAsync(int orderId);
        
        // Các phương thức bổ sung cho Dashboard
        Task<int> GetTotalOrderCountAsync();
        Task<int> GetTotalOrderCountAsync(string? searchTerm, string? status, DateTime? fromDate, DateTime? toDate);
        Task<IEnumerable<Order>> GetRecentOrdersAsync(int count);
        Task<IEnumerable<OrderSummaryDto>> GetOrderSummariesAsync(OrderStatus? status = null);
        Task<Dictionary<string, decimal>> GetMonthlyRevenueAsync(int months);
        
        // Các phương thức bổ sung cho báo cáo
        Task<IEnumerable<Order>> GetOrdersInPeriodAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<TopCustomerData>> GetTopCustomersAsync(DateTime startDate, DateTime endDate, int count);
    }
}