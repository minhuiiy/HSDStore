using System.ComponentModel.DataAnnotations;

namespace CPTStore.Areas.Admin.ViewModels
{
    public class OrderConfirmationSettingsViewModel
    {
        [Display(Name = "Bật tự động xác nhận đơn hàng")]
        public bool AutoConfirmEnabled { get; set; }

        [Display(Name = "Thời gian chờ trước khi xác nhận (phút)")]
        [Range(1, 1440, ErrorMessage = "Thời gian chờ phải từ 1 đến 1440 phút (24 giờ)")]
        public int ConfirmationDelayMinutes { get; set; }

        [Display(Name = "Loại trừ đơn hàng COD")]
        public bool ExcludeCODOrders { get; set; }

        [Display(Name = "Loại trừ đơn hàng giá trị cao")]
        public bool ExcludeHighValueOrders { get; set; }

        [Display(Name = "Ngưỡng giá trị cao (VND)")]
        [Range(0, 100000000, ErrorMessage = "Ngưỡng giá trị phải từ 0 đến 100,000,000 VND")]
        public decimal HighValueThreshold { get; set; }

        [Display(Name = "Cập nhật lần cuối")]
        public DateTime LastUpdated { get; set; }
    }
}