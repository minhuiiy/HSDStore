using Microsoft.AspNetCore.Mvc;
using CPTStore.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CPTStore.ViewComponents
{
    public class CartItemsViewComponent : ViewComponent
    {
        /// <summary>
        /// Hiển thị danh sách sản phẩm trong giỏ hàng
        /// </summary>
        /// <param name="cartItems">Danh sách CartItem</param>
        /// <param name="showControls">Có hiển thị các nút điều khiển (tăng/giảm số lượng, xóa) hay không</param>
        /// <returns>View hiển thị danh sách sản phẩm</returns>
        public async Task<IViewComponentResult> InvokeAsync(List<CartItem>? cartItems, bool showControls = false)
        {
            // Có thể thêm logic xử lý ở đây, ví dụ: tính tổng tiền, kiểm tra tồn kho, v.v.
            
            ViewBag.ShowControls = showControls;
            
            return await Task.FromResult(View(cartItems));
        }
    }
}