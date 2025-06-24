using System.Collections.Generic;

namespace CPTStore.Resources
{
    /// <summary>
    /// Lớp quản lý các chuỗi văn bản trong ứng dụng
    /// Giúp tập trung các chuỗi văn bản và dễ dàng đa ngôn ngữ sau này
    /// </summary>
    public static class StringResources
    {
        // Chung
        public static string AppName => "CPTStore";
        public static string Currency => "VNĐ";
        
        // Giỏ hàng
        public static class Cart
        {
            public static string Title => "Giỏ hàng";
            public static string EmptyCart => "Giỏ hàng của bạn đang trống";
            public static string AddMoreProducts => "Hãy thêm sản phẩm vào giỏ hàng để tiến hành mua sắm";
            public static string ContinueShopping => "Tiếp tục mua sắm";
            public static string ClearCart => "Xóa giỏ hàng";
            public static string OrderSummary => "Tóm tắt đơn hàng";
            public static string Subtotal => "Tạm tính:";
            public static string ShippingFee => "Phí vận chuyển:";
            public static string Total => "Tổng cộng:";
            public static string Checkout => "Tiến hành thanh toán";
            public static string CouponCode => "Mã giảm giá";
            public static string ApplyCoupon => "Áp dụng";
            public static string EnterCouponCode => "Nhập mã giảm giá";
            public static string FreeShipping => "Miễn phí";
        }
        
        // Thanh toán
        public static class Checkout
        {
            public static string Title => "Thanh toán";
            public static string BillingInformation => "Thông tin thanh toán";
            public static string OrderDetails => "Chi tiết đơn hàng";
            public static string PaymentMethod => "Phương thức thanh toán";
            public static string CashOnDelivery => "Thanh toán khi nhận hàng (COD)";
            public static string BankTransfer => "Chuyển khoản ngân hàng";
            public static string CreditCard => "Thẻ tín dụng/Ghi nợ";
            public static string PlaceOrder => "Đặt hàng";
            public static string Notes => "Ghi chú";
            public static string OrderNotesPlaceholder => "Ghi chú về đơn hàng, ví dụ: thời gian hay chỉ dẫn địa điểm giao hàng chi tiết";
        }
        
        // Thông báo
        public static class Notifications
        {
            public static string AddToCartSuccess => "Sản phẩm đã được thêm vào giỏ hàng";
            public static string RemoveFromCartSuccess => "Sản phẩm đã được xóa khỏi giỏ hàng";
            public static string UpdateCartSuccess => "Giỏ hàng đã được cập nhật";
            public static string ClearCartSuccess => "Giỏ hàng đã được xóa";
            public static string OrderSuccess => "Đặt hàng thành công";
            public static string CouponApplied => "Mã giảm giá đã được áp dụng";
            public static string InvalidCoupon => "Mã giảm giá không hợp lệ";
        }
    }
}