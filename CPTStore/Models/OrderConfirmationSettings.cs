using System;

namespace CPTStore.Models
{
    public class OrderConfirmationSettings
    {
        public bool AutoConfirmEnabled { get; set; } = false;
        public int ConfirmationDelayMinutes { get; set; } = 30; // Thời gian chờ trước khi tự động xác nhận
        public bool ExcludeCODOrders { get; set; } = false; // Có loại trừ đơn hàng COD không
        public bool ExcludeHighValueOrders { get; set; } = false; // Có loại trừ đơn hàng giá trị cao không
        public decimal HighValueThreshold { get; set; } = 5000000; // Ngưỡng giá trị cao (mặc định 5 triệu VND)
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}