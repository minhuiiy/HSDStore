using CPTStore.Models;

namespace CPTStore.Extensions
{
    public static class CartItemExtensions
    {
        /// <summary>
        /// Lấy tên sản phẩm từ CartItem
        /// </summary>
        /// <param name="item">CartItem</param>
        /// <returns>Tên sản phẩm hoặc thông báo mặc định nếu không có sản phẩm</returns>
        public static string GetProductName(this CartItem item)
        {
            return item.Product?.Name ?? "Sản phẩm không xác định";
        }
        
        /// <summary>
        /// Lấy URL hình ảnh từ CartItem
        /// </summary>
        /// <param name="item">CartItem</param>
        /// <returns>URL hình ảnh hoặc null nếu không có</returns>
        public static string? GetImageUrl(this CartItem item)
        {
            return item.Product?.ImageUrl;
        }
        
        /// <summary>
        /// Lấy giá sản phẩm từ CartItem
        /// </summary>
        /// <param name="item">CartItem</param>
        /// <returns>Giá sản phẩm</returns>
        public static decimal GetPrice(this CartItem item)
        {
            return item.UnitPrice;
        }
        
        /// <summary>
        /// Tính tổng giá trị của CartItem
        /// </summary>
        /// <param name="item">CartItem</param>
        /// <returns>Tổng giá trị (số lượng * đơn giá)</returns>
        public static decimal GetTotalPrice(this CartItem item)
        {
            return item.Quantity * item.UnitPrice;
        }
    }
}