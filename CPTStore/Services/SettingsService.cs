using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace CPTStore.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SettingsService> _logger;
        private const string ORDER_CONFIRMATION_SETTINGS_KEY = "OrderConfirmationSettings";

        public SettingsService(ApplicationDbContext context, ILogger<SettingsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<OrderConfirmationSettings> GetOrderConfirmationSettingsAsync()
        {
            try
            {
                var settingEntity = await _context.Settings
                    .FirstOrDefaultAsync(s => s.Key == ORDER_CONFIRMATION_SETTINGS_KEY);

                if (settingEntity == null)
                {
                    // Nếu không tìm thấy cài đặt, tạo cài đặt mặc định
                    var defaultSettings = new OrderConfirmationSettings();
                    await SaveOrderConfirmationSettingsAsync(defaultSettings);
                    return defaultSettings;
                }

                // Chuyển đổi chuỗi JSON thành đối tượng OrderConfirmationSettings
                return JsonSerializer.Deserialize<OrderConfirmationSettings>(settingEntity.Value) 
                    ?? new OrderConfirmationSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy cài đặt tự động xác nhận đơn hàng.");
                return new OrderConfirmationSettings(); // Trả về cài đặt mặc định nếu có lỗi
            }
        }

        public async Task SaveOrderConfirmationSettingsAsync(OrderConfirmationSettings settings)
        {
            try
            {
                // Cập nhật thời gian cập nhật
                settings.LastUpdated = DateTime.Now;

                // Chuyển đổi đối tượng thành chuỗi JSON
                string jsonValue = JsonSerializer.Serialize(settings);

                // Tìm cài đặt hiện có hoặc tạo mới
                var settingEntity = await _context.Settings
                    .FirstOrDefaultAsync(s => s.Key == ORDER_CONFIRMATION_SETTINGS_KEY);

                if (settingEntity == null)
                {
                    // Tạo mới nếu không tìm thấy
                    settingEntity = new Setting
                    {
                        Key = ORDER_CONFIRMATION_SETTINGS_KEY,
                        Value = jsonValue,
                        Description = "Cài đặt tự động xác nhận đơn hàng"
                    };
                    _context.Settings.Add(settingEntity);
                }
                else
                {
                    // Cập nhật nếu đã tồn tại
                    settingEntity.Value = jsonValue;
                    _context.Settings.Update(settingEntity);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã lưu cài đặt tự động xác nhận đơn hàng.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu cài đặt tự động xác nhận đơn hàng.");
                throw;
            }
        }
    }
}