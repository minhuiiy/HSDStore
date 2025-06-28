using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CPTStore.Services.BackgroundServices
{
    public class OrderConfirmationService : BackgroundService
    {
        private readonly ILogger<OrderConfirmationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private TimeSpan _period = TimeSpan.FromMinutes(30); // Mặc định chạy mỗi 30 phút

        public OrderConfirmationService(
            ILogger<OrderConfirmationService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dịch vụ tự động xác nhận đơn hàng đã bắt đầu.");

            using var timer = new PeriodicTimer(_period);

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessPendingOrdersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý tự động xác nhận đơn hàng.");
                }
            }
        }

        private async Task ProcessPendingOrdersAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Bắt đầu xử lý các đơn hàng đang chờ xác nhận...");

            using var scope = _serviceProvider.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();

            // Lấy cài đặt tự động xác nhận đơn hàng
            var settings = await settingsService.GetOrderConfirmationSettingsAsync();

            // Kiểm tra xem tính năng tự động xác nhận có được bật không
            if (!settings.AutoConfirmEnabled)
            {
                _logger.LogInformation("Tính năng tự động xác nhận đơn hàng đang bị tắt.");
                return;
            }

            // Cập nhật chu kỳ chạy dựa trên cài đặt
            _period = TimeSpan.FromMinutes(settings.ConfirmationDelayMinutes);

            // Lấy tất cả đơn hàng đang ở trạng thái Pending
            var query = dbContext.Orders.Where(o => o.Status == OrderStatus.Pending);

            // Áp dụng các bộ lọc dựa trên cài đặt
            if (settings.ExcludeCODOrders)
            {
                query = query.Where(o => o.PaymentMethod != PaymentMethod.COD);
            }

            if (settings.ExcludeHighValueOrders)
            {
                query = query.Where(o => o.TotalAmount < settings.HighValueThreshold);
            }

            // Chỉ xử lý các đơn hàng đã tạo ít nhất [ConfirmationDelayMinutes] phút trước
            var cutoffTime = DateTime.Now.AddMinutes(-settings.ConfirmationDelayMinutes);
            query = query.Where(o => o.CreatedAt <= cutoffTime);

            var pendingOrders = await query.ToListAsync(stoppingToken);

            _logger.LogInformation($"Tìm thấy {pendingOrders.Count} đơn hàng đủ điều kiện để tự động xác nhận.");

            // Danh sách các đơn hàng đã được xác nhận thành công
            var confirmedOrders = new List<Order>();

            foreach (var order in pendingOrders)
            {
                try
                {
                    // Cập nhật trạng thái đơn hàng từ Pending sang Processing
                    await orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Processing);
                    _logger.LogInformation($"Đã tự động xác nhận đơn hàng #{order.OrderNumber} (ID: {order.Id}).");
                    confirmedOrders.Add(order);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lỗi khi tự động xác nhận đơn hàng #{order.OrderNumber} (ID: {order.Id}).");
                }
            }

            _logger.LogInformation($"Đã tự động xác nhận {confirmedOrders.Count}/{pendingOrders.Count} đơn hàng.");
        }
    }
}