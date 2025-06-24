using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using CPTStore.Services.Interfaces;
using CPTStore.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CPTStore.Areas.Admin.ViewModels;
using Microsoft.Extensions.Logging;

namespace CPTStore.Areas.Admin.Controllers
{
    public class OrdersController : AdminControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        // GET: Admin/Orders
        public async Task<IActionResult> Index(string searchTerm, string status, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 10)
        {
            try
            {
                var orders = await _orderService.GetOrdersAsync(searchTerm, status, fromDate, toDate, page, pageSize);

                ViewBag.SearchTerm = searchTerm;
                ViewBag.Status = status;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                
                // Lấy tổng số đơn hàng để tính số trang
                int totalOrders;
                try
                {
                    totalOrders = await _orderService.GetTotalOrderCountAsync(searchTerm, status, fromDate, toDate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi lấy tổng số đơn hàng: {Message}", ex.Message);
                    // Nếu có lỗi khi lấy tổng số đơn hàng, đặt giá trị mặc định
                    totalOrders = 0;
                }
                
                ViewBag.TotalPages = (int)Math.Ceiling(totalOrders / (double)pageSize);
                ViewBag.TotalOrders = totalOrders;

                // Lấy danh sách trạng thái đơn hàng để hiển thị trong dropdown
                ViewBag.OrderStatuses = Enum.GetValues(typeof(OrderStatus))
                    .Cast<OrderStatus>()
                    .Select(s => new SelectListItem
                    {
                        Value = s.ToString(),
                        Text = s.ToString()
                    }).ToList();

                return View(orders);
            }
            catch (Exception ex)
            {
                // Ghi log lỗi
                _logger.LogError(ex, "Đã xảy ra lỗi khi tải danh sách đơn hàng: {Message}", ex.Message);
                
                // Xử lý lỗi và hiển thị thông báo lỗi
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải danh sách đơn hàng: " + ex.Message;
                
                // Lấy danh sách trạng thái đơn hàng để hiển thị trong dropdown ngay cả khi có lỗi
                ViewBag.OrderStatuses = Enum.GetValues(typeof(OrderStatus))
                    .Cast<OrderStatus>()
                    .Select(s => new SelectListItem
                    {
                        Value = s.ToString(),
                        Text = s.ToString()
                    }).ToList();
                    
                return View(new List<Order>());
            }
        }

        // GET: Admin/Orders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var order = await _orderService.GetOrderByIdAsync(id.Value);
                if (order == null)
                {
                    return NotFound();
                }

                return View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết đơn hàng ID {OrderId}: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải chi tiết đơn hàng: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Admin/Orders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var order = await _orderService.GetOrderByIdAsync(id.Value);
                if (order == null)
                {
                    return NotFound();
                }

                // Lấy danh sách trạng thái đơn hàng để hiển thị trong dropdown
                ViewBag.OrderStatuses = Enum.GetValues(typeof(OrderStatus))
                    .Cast<OrderStatus>()
                    .Select(s => new SelectListItem
                    {
                        Value = s.ToString(),
                        Text = s.ToString(),
                        Selected = s.ToString() == order.Status.ToString()
                    }).ToList();
                    
                return View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin đơn hàng ID {OrderId} để chỉnh sửa: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải thông tin đơn hàng: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Admin/Orders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Status,Notes")] Order order)
        {
            if (id != order.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Lấy đơn hàng hiện tại từ cơ sở dữ liệu
                    var existingOrder = await _orderService.GetOrderByIdAsync(id);
                    if (existingOrder == null)
                    {
                        return NotFound();
                    }

                    // Cập nhật thông tin đơn hàng
                    existingOrder.Status = order.Status;
                    existingOrder.Notes = order.Notes;
                    existingOrder.UpdatedAt = DateTime.Now;

                    // Lưu thay đổi
                    await _orderService.UpdateOrderAsync(existingOrder);
                    
                    TempData["SuccessMessage"] = "Đơn hàng đã được cập nhật thành công.";
                    return RedirectToAction(nameof(Details), new { id = existingOrder.Id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi cập nhật đơn hàng ID {OrderId}: {Message}", id, ex.Message);
                    
                    if (!await OrderExists(order.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        ModelState.AddModelError("", "Đã xảy ra lỗi khi cập nhật đơn hàng: " + ex.Message);
                    }
                }
            }

            // Nếu ModelState không hợp lệ, chuẩn bị lại danh sách trạng thái
            ViewBag.OrderStatuses = Enum.GetValues(typeof(OrderStatus))
                .Cast<OrderStatus>()
                .Select(s => new SelectListItem
                {
                    Value = s.ToString(),
                    Text = s.ToString(),
                    Selected = s.ToString() == order.Status.ToString()
                }).ToList();
                
            return View(order);
        }

        // GET: Admin/Orders/UpdateStatus/5
        public async Task<IActionResult> UpdateStatus(int id)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(id);
                if (order == null)
                {
                    return NotFound();
                }

                // Lấy danh sách trạng thái đơn hàng để hiển thị trong dropdown
                ViewBag.OrderStatuses = Enum.GetValues(typeof(OrderStatus))
                    .Cast<OrderStatus>()
                    .Select(s => new SelectListItem
                    {
                        Value = s.ToString(),
                        Text = s.ToString(),
                        Selected = s.ToString() == order.Status.ToString()
                    }).ToList();

                return View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin đơn hàng ID {OrderId} để cập nhật trạng thái: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải thông tin đơn hàng: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Admin/Orders/UpdateStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(id);
                if (order == null)
                {
                    return NotFound();
                }

                order.Status = Enum.Parse<OrderStatus>(status);
                order.UpdatedAt = DateTime.Now;

                await _orderService.UpdateOrderAsync(order);
                
                TempData["SuccessMessage"] = "Trạng thái đơn hàng đã được cập nhật thành công.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật trạng thái đơn hàng ID {OrderId}: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi cập nhật trạng thái đơn hàng: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id = id });
            }
        }

        // GET: Admin/Orders/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(id);
                if (order == null)
                {
                    return NotFound();
                }

                // Chỉ cho phép xóa đơn hàng ở trạng thái Pending
                if (order.Status != OrderStatus.Pending)
                {
                    TempData["ErrorMessage"] = "Chỉ có thể xóa đơn hàng ở trạng thái chờ xử lý.";
                    return RedirectToAction(nameof(Details), new { id = order.Id });
                }

                return View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin đơn hàng ID {OrderId} để xóa: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải thông tin đơn hàng: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Admin/Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(id);
                if (order == null)
                {
                    return NotFound();
                }

                // Chỉ cho phép xóa đơn hàng ở trạng thái Pending
                if (order.Status != OrderStatus.Pending)
                {
                    TempData["ErrorMessage"] = "Chỉ có thể xóa đơn hàng ở trạng thái chờ xử lý.";
                    return RedirectToAction(nameof(Index));
                }

                await _orderService.DeleteOrderAsync(id);
                TempData["SuccessMessage"] = "Đơn hàng đã được xóa thành công.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa đơn hàng ID {OrderId}: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi xóa đơn hàng: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task<bool> OrderExists(int id)
        {
            return await _orderService.GetOrderByIdAsync(id) != null;
        }
    }
}